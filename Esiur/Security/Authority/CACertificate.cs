using Esiur.Data;
using Esiur.Engine;
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

namespace Esiur.Security.Authority
{
    public class CACertificate : Certificate
    {

        string name;

        public string Name
        {
            get { return name; }
        }

        public CACertificate(byte[] data, uint offset, uint length, bool privateKeyIncluded = false)
            :base(0, DateTime.MinValue, DateTime.MinValue, HashFunctionType.MD5)
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
            
            cr.Append(id, issueDate, expireDate);

            // hash function
            cr.Append((byte)((byte)hashFunction << 4));
            this.hashFunction = hashFunction;

            // CA Name
            this.name = authorityName;
            cr.Append((byte)(authorityName.Length), Encoding.ASCII.GetBytes(authorityName));

            // public key
            rsa = RSA.Create();// new RSACryptoServiceProvider(2048);
            rsa.KeySize = 2048;
            RSAParameters dRSAKey = rsa.ExportParameters(true);


            cr.Append((byte)dRSAKey.Exponent.Length, dRSAKey.Exponent, (ushort)dRSAKey.Modulus.Length, dRSAKey.Modulus);


            publicRawData = cr.ToArray();

            privateRawData = DC.Merge(dRSAKey.D, dRSAKey.DP, dRSAKey.DQ, dRSAKey.InverseQ, dRSAKey.P, dRSAKey.Q);


        }

        public override bool Save(string filename, bool includePrivate = false)
        {
            try
            {
                if (includePrivate)
                    File.WriteAllBytes(filename, BinaryList.ToBytes((byte)CertificateType.CAPrivate, publicRawData, privateRawData));
                else
                    File.WriteAllBytes(filename, BinaryList.ToBytes((byte)CertificateType.CAPublic, publicRawData));

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
                return BinaryList.ToBytes(publicRawData, privateRawData);
            else
                return publicRawData;
        }

    }
}
