using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Esiur.Analysis.Graph
{
    public class Matrix<T> where T : struct
    {
        internal T[,] value;

        public int Rows { get; private set; }
        public int Columns { get; private set; }

        public T this[int x, int y] {
            get => value [x, y];
            set => this.value[x, y] = value;
        }

        public Matrix(int rows, int columns)
        {
            value = new T[rows, columns];
        }

        public Matrix(T[,] value)
        {
            this.value = value;
            Rows = value.GetLength(0);
            Columns = value.GetLength(1); 
        }

        public Matrix<T> Transpose()
        {
            var rt = new T[Columns, Rows];

            for(var i = 0; i < Rows; i++)
                for(var j = 0; j < Columns; j++)
                    rt[j, i] = value[i, j];

            return new Matrix<T>(rt);
        }

        public static Matrix<T> operator *(Matrix<T> a, Matrix<T> b)
            => Multiply(a, b);

        public  Matrix<T> Pow(int power)
        {
            var rt = this;
            for (var i = 1; i < power; i++)
                rt = rt * rt;
            return rt;
        }

        public override string ToString()
        {
            var rt = "";
            for (var i = 0; i < Rows; i++)
            {
                rt += "|";

                for (var j = 0; j < Columns - 1; j++)
                    rt += value[i, j] + ",";

                rt += value[i, Columns - 1] + "|";
            }

            return rt;
        }

        public static Matrix<T> Multiply<T>(Matrix<T> a, Matrix<T> b) where T : struct
        {


            if (a.Columns != b.Rows)
                throw new ArgumentException("`a` rows do not match `b` columns.");


            T[,] rt = new T[a.Rows, b.Columns];

            for (int i = 0; i < a.Rows; i++)
            { // aRow
                for (int j = 0; j < b.Columns; j++)
                { // bColumn
                    for (int k = 0; k < a.Columns; k++)
                    { // aColumn
                        dynamic aValue = a[i, k];
                        dynamic bValue = b[k, j];
                        rt[i, j] += aValue * bValue;// a[i, k] * b[k, j];
                    }
                }
            }

            return new Matrix<T>(rt);
        }
    }
}
