using Esiur.Analysis.Graph;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Coding
{
    internal class BlockCode<T> where T : struct
    {
        public Matrix<T> Generator { get; private set; }
        public Matrix<T> ParityCheck { get; private set; }

        int k = 0;
        Matrix<T> p;

        public BlockCode(Matrix<T> parityMatrix, T identity)
        {
            // create generator matrix from parity matrix
            // K = number of rows

            p = parityMatrix;
            k = parityMatrix.Rows;

            var g = new Matrix<T>(p.Rows, p.Columns + p.Rows);
            for(var i = 0; i < p.Rows; i++)
                for(var j = 0; j < p.Columns; j++)
                    g[i, j] = p[i, j];

            //for (var i = 0; i < k; i++)
            //    g[i, p.Columns + i] = (T)1;

        }
    }
}
