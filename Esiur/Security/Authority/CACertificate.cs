/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Esiur.Data;
using Esiur.Core;
using Esiur.Misc;
using Esiur.Security.Cryptography;
using Esiur.Security.Integrity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Security.Authority;

public class CACertificate : Certificate
{

    string name;

    public string Name
    {
        get { return name; }
    }

    public CACertificate(byte[] data, uint offset, uint length, bool privateKeyIncluded = false)
        : base(0, DateTime.MinValue, DateTime.MinValue, HashFunctionType.MD5)
    {

        uint oOffset = offset;

        this.id = DC.GetUInt64(data, offset);
        offset += 8;
        this.issueDate = DC.GetDateTime(data, offset);
        offset += 8;
        this.expireDate = DC.GetDateTime(data, offset);
        offset += 8;
        this.hashFunction = (HashFunctionType)(data[offset++] >> 4);


        this.name = (Encoding.ASCII.GetString(data, (int)offset + 1, data[offset]));
        offset += (uint)data[offset] + 1;


        var aea = (AsymetricEncryptionAlgorithmType)(data[offset] >> 5);

        if (aea == AsymetricEncryptionAlgorithmType.RSA)
        {
            var key = new RSAParameters();
            uint exponentLength = (uint)data[offset++] & 0x1F;

            key.Exponent = DC.Clip(data, offset, exponentLength);

            offset += exponentLength;

            uint keySize = DC.GetUInt16(data, offset);
            offset += 2;

            key.Modulus = DC.Clip(data, offset, keySize);

            offset += keySize;

            // copy cert data
            this.publicRawData = new byte[offset - oOffset];
            Buffer.BlockCopy(data, (int)oOffset, publicRawData, 0, publicRawData.Length);

            if (privateKeyIncluded)
            {
                uint privateKeyLength = (keySize * 3) + (keySize / 2);
                uint halfKeySize = keySize / 2;

                privateRawData = DC.Clip(data, offset, privateKeyLength);

                key.D = DC.Clip(data, offset, keySize);
                offset += keySize;

                key.DP = DC.Clip(data, offset, halfKeySize);
                offset += halfKeySize;

                key.DQ = DC.Clip(data, offset, halfKeySize);
                offset += halfKeySize;


                key.InverseQ = DC.Clip(data, offset, halfKeySize);
                offset += halfKeySize;

                key.P = DC.Clip(data, offset, halfKeySize);
                offset += halfKeySize;

                key.Q = DC.Clip(data, offset, halfKeySize);
                offset += halfKeySize;

            }

            // setup rsa
            this.rsa = RSA.Create();// new RSACryptoServiceProvider();
            this.rsa.ImportParameters(key);
        }
    }

    public CACertificate(ulong id, string authorityName, DateTime issueDate, DateTime expireDate,
                            HashFunctionType hashFunction = HashFunctionType.SHA1, uint ip = 0, byte[] ip6 = null)
        : base(id, issueDate, expireDate, hashFunction)
    {
        // assign type

        BinaryList cr = new BinaryList();

        // make header

        cr.AddUInt64(id)
            .AddDateTime(issueDate)
            .AddDateTime(expireDate);


        // hash function
        cr.AddUInt8((byte)((byte)hashFunction << 4));
        this.hashFunction = hashFunction;

        // CA Name
        this.name = authorityName;
        cr.AddUInt8((byte)(authorityName.Length))
          .AddUInt8Array(Encoding.ASCII.GetBytes(authorityName));

        // public key
        rsa = RSA.Create();// new RSACryptoServiceProvider(2048);
        rsa.KeySize = 2048;
        RSAParameters dRSAKey = rsa.ExportParameters(true);


        cr.AddUInt8((byte)dRSAKey.Exponent.Length)
            .AddUInt8Array(dRSAKey.Exponent)
            .AddUInt16((ushort)dRSAKey.Modulus.Length)
            .AddUInt8Array(dRSAKey.Modulus);


        publicRawData = cr.ToArray();

        privateRawData = DC.Merge(dRSAKey.D, dRSAKey.DP, dRSAKey.DQ, dRSAKey.InverseQ, dRSAKey.P, dRSAKey.Q);

    }

    public override bool Save(string filename, bool includePrivate = false)
    {
        try
        {
            if (includePrivate)
                File.WriteAllBytes(filename, new BinaryList()
                                                    .AddUInt8((byte)CertificateType.CAPrivate)
                                                    .AddUInt8Array(publicRawData)
                                                    .AddUInt8Array(privateRawData)
                                                    .ToArray());
            else
                File.WriteAllBytes(filename, new BinaryList()
                                                    .AddUInt8((byte)CertificateType.CAPublic)
                                                    .AddUInt8Array(publicRawData).ToArray());

            return true;
        }
        catch
        {
            return false;
        }
    }

    public override byte[] Serialize(bool includePrivate = false)
    {
        if (includePrivate)
            return new BinaryList()
                    .AddUInt8Array(publicRawData)
                    .AddUInt8Array(privateRawData)
                    .ToArray();
        else
            return publicRawData;
    }

}
