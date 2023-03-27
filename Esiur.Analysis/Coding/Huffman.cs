using Esiur.Analysis.Units;
using Esiur.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Esiur.Analysis.Coding
{
    public class Huffman<T> : IStreamCodec<T> where T : System.Enum
    {

        public CodeSet<T> CodeSet { get; } = new CodeSet<T>();

        public class Node<TKey, TValue, TFrequency>
        {
            public TKey Key { get; set; } // decision maker (bit)


            public TValue Value { get; set; } // node value
            public TFrequency Frequency { get; set; } // node / subnodes frequency

            public Dictionary<TKey, Node<TKey, TValue, TFrequency>> Branches { get; set; }


            public Node<TKey, TValue, TFrequency> Parent { get; set; }


            public override string ToString()
            {
                if (Sequence != null)
                    return $"{Key} => {Value} [{Frequency}] | {string.Join("->", Sequence)}";
                else
                    return $"{Key} => {Value} [{Frequency}]";
            }


            public TKey[]? Sequence { get; internal set; }

            public void UpdateDecisionSequence()
            {
                var parent = Parent;

                var seq = new List<TKey>() { Key };

                while (parent != null)
                {
                    seq.Add(parent.Key);
                    parent = parent.Parent;
                }

                seq.Reverse();

                Sequence = seq.ToArray();
            }
        }


        public class Tree<TKey, TValue, TFrequency>
        {

            public (uint, TValue) Decide(TKey[] sequence, uint offset)
            {
                var oOffset = offset;

                var node = Branches[sequence[offset++]];

                while (node != null && node.Branches != null && node.Branches.Count > 0)
                {
                    node = node.Branches[sequence[offset++]];
                }

                return (offset - oOffset, node.Value);
            }

            public Dictionary<TKey, Node<TKey, TValue, TFrequency>> Branches { get; }
            public Dictionary<TValue, Node<TKey, TValue, TFrequency>> Leafs { get; set; }

            public Tree(Node<TKey, TValue, TFrequency> rootNode)
            {
                Branches = rootNode.Branches;

                var leafs = new List<Node<TKey, TValue, TFrequency>>();

                GetLeafs(rootNode, leafs, new TKey[0]);

                Leafs = leafs.ToDictionary(x => x.Value, x => x);
            }

            void GetLeafs(Node<TKey, TValue, TFrequency> node, List<Node<TKey, TValue, TFrequency>> leafs, TKey[] sequence)
            {
                foreach (var branch in node.Branches)
                    if (branch.Value.Branches == null || branch.Value.Branches.Count == 0)
                    {
                        leafs.Add(branch.Value);
                        branch.Value.Sequence = sequence.Append(branch.Key).ToArray();
                    }
                    else
                    {
                        GetLeafs(branch.Value, leafs, sequence.Append(branch.Key).ToArray());
                    }
            }
        }




        public Tree<T, CodeWord<T>, int> DecisionTree { get; set; }

        public Huffman(CodeWord<T>[] source, uint offset, uint length)
        {
            //var freq = new int[byte.MaxValue + 1];

            var freq = new Dictionary<CodeWord<T>, int>();

            // var root = new Branch<bool, KeyValuePair<byte, int>>();

            // calculate probabilities
            var end = offset + length;
            for (var i = offset; i < end; i++)
                if (freq.ContainsKey(source[i]))
                    freq[source[i]]++;
                else
                    freq.Add(source[i], 1);

            var nodes = freq.OrderBy(x => x.Value).Select(x => new Node<T, CodeWord<T>, int>()
            { Frequency = x.Value, Key = default(T), Value = x.Key }).ToList();


            //var leafs = nodes.ToList();

            while (nodes.Count() > 1)
            {
                var decision = nodes.Take(CodeSet.ElementsCount).ToList();

                //decision[1].Key = true;

                var branch = new Node<T, CodeWord<T>, int>
                {
                    Branches = new Dictionary<T, Node<T, CodeWord<T>, int>>(),
                    //{
                    //    [decision[0].Key] = decision[0],
                    //    [decision[1].Key] = decision[1]
                    //},
                    Key = CodeSet.Elements.First(),
                    Frequency = decision[0].Frequency + decision[1].Frequency
                };


                // assign values
                for (var i = 0; i < decision.Count; i++)
                {
                    branch.Branches.Add(CodeSet.Elements[i], decision[i]);
                    decision[i].Key = CodeSet.Elements[i];
                }

                decision[0].Parent = branch;
                decision[1].Parent = branch;

                nodes = nodes.Skip(2).Append(branch).OrderBy(x => x.Frequency).ToList();
            }

            // create tree

            DecisionTree = new Tree<T, CodeWord<T>, int>(nodes[0]);

            Console.WriteLine();

        }

        public T[] Encode(CodeWord<T>[] source, uint offset, uint length)
        {
            var rt = new List<T>();
            var end = offset + length;

            for(var i = offset; i < end; i++)
            {
                rt.AddRange(DecisionTree.Leafs[source[i]].Sequence);
            }

            return rt.ToArray();
        }

        public CodeWord<T>[] Decode(T[] source, uint offset, uint length)
        {
            var rt = new List<CodeWord<T>>();

            uint processed = 0;
            while (processed < length)
            {
                var (len, value) = DecisionTree.Decide(source, offset);
                rt.Add(value);
                processed += len;
                offset += len;
            }

            return rt.ToArray();

        }

        //public byte[] Encode(byte[] source, uint offset, uint length)
        //{
        //    var end = offset + length;

        //    var seq = new List<T>();
        //    for (var i = offset; i < end; i++)
        //    {
        //        seq.AddRange(DecisionTree.Leafs[source[i]].Sequence);
        //    }


        //    //var str = (String.Join("", seq.Select(x => x ? "1" : "0")));

        //    // convert sequence to bytes

        //    var rt = new byte[(seq.Count - 1) / 8 + 1];
        //    var dst = 0;

        //    for (var i = 0; i < rt.Length; i++)
        //    {
        //        for (var j = 7; j >= 0; j--)
        //        {
        //            if (dst >= seq.Count)
        //                break;

        //            if (seq[dst++])
        //                rt[i] |= (byte)(0x1 << j);
        //        }
        //    }

        //    // bits.CopyTo(rt, 0);
        //    return rt;
        //}

        //public T[] Decode(T[] source, uint offset, uint length)
        //{

        //    var rt = new List<byte>();

        //    var bits = new bool[length * 8];
        //    var end = offset + length;



        //    var dst = 0;
        //    for (var i = offset; i < end; i++)
        //    {
        //        for (var j = 7; j >= 0; j--)
        //        {
        //            bits[dst++] = ((source[i] >> j) & 0x1) > 0 ? true : false;
        //        }
        //    }

        //    uint b = 0;
        //    while (b < bits.Length)
        //    {
        //        var (len, value) = DecisionTree.Decide(bits, b);
        //        rt.Add(value);
        //        b += len;
        //    }

        //    return rt.ToArray();
        //}
    }
}
