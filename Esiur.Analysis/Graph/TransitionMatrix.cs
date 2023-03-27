using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Esiur.Analysis.Graph
{
    public class Matrix<T> where T : struct
    {
        internal T[,] value;

        public int Rows => value.GetLength(0);
        public int Columns => value.GetLength(1);

        public T this[int x, int y] => value[x, y];

        public Matrix(T[,] value)
        {
            this.value = value;
        }

        public static Matrix<T> Multiply<T>(Matrix<T> a, Matrix<T> b) where T:struct
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
                        rt[i, j] += a[i, k] * b[k, j];
                    }
                }
            }

            return new Matrix<T>(rt);
        }
    }
}
