﻿using Esiur.Data;
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
    public class DomainCertificate : Certificate
    {
        uint ip;
        byte[] ip6;
        string domain;

        //CACertificate ca;
        string caName;
        ulong caId;
        byte[] signature;

        string authorityName;

        public string AuthorityName
        {
            get { return authorityName; }
        }

        public string Domain
        {
            get { return domain; }
        }

        public byte[] Signature
        {
            get { return signature; }
        }

        public uint IPAddress
        {
            get { return ip; }
        }

        public byte[] IPv6Address
        {
            get { return ip6; }
        }

        public DomainCertificate(byte[] data, uint offset, uint length, bool privateKeyIncluded = false)
            :base(0, DateTime.MinValue, DateTime.MinValue, HashFunctionType.MD5)
        {
            var oOffset = offset;

            this.id = DC.GetUInt64(data, offset);
            offset += 8;

            // load IPs
            this.ip = DC.GetUInt32(data, offset);
            offset += 4;
            this.ip6 = DC.Clip(data, offset, 16);

            offset += 16;

            this.issueDate = DC.GetDateTime(data, offset);
            offset += 8;
            this.expireDate = DC.GetDateTime(data, offset);
            offset += 8;

            this.domain = Encoding.ASCII.GetString(data, (int)offset + 1, data[offset]);
            offset += (uint)data[offset] + 1;

            this.authorityName = (Encoding.ASCII.GetString(data, (int)offset + 1, data[offset]));
            offset += (uint)data[offset] + 1;

            caId = DC.GetUInt64(data, offset);
            offset += 8;

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
                publicRawData = new byte[offset - oOffset];
                Buffer.BlockCopy(data, (int)oOffset, publicRawData, 0, publicRawData.Length);

                if (privateKeyIncluded)
                {

                    uint privateKeyLength = (keySize * 3) + (keySize / 2);
                    privateRawData = DC.Clip(data, offset, privateKeyLength);

                    uint halfKeySize = keySize / 2;

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
                rsa = RSA.Create();// new RSACryptoServiceProvider();
                rsa.ImportParameters(key);

                this.signature = DC.Clip(data, offset, length - (offset - oOffset));

            }

        }

        public DomainCertificate(ulong id, string domain, CACertificate authority, DateTime issueDate,
            DateTime expireDate, HashFunctionType hashFunction = HashFunctionType.SHA1, uint ip = 0, byte[] ip6 = null)
            : base (id, issueDate, expireDate, hashFunction)
        {
            // assign type

             var cr = new BinaryList();

            // id
            cr.Append(id);

            // ip
            this.ip = ip;
            this.ip6 = ip6;

            cr.Append(ip);

            
            if (ip6?.Length == 16)
                cr.Append(ip6);
            else
                cr.Append(new byte[16]);


            cr.Append(issueDate, expireDate);

            // domain
            this.domain = domain;
            cr.Append((byte)(domain.Length), Encoding.ASCII.GetBytes(domain));

            // CA
            this.caName = authority.Name;
            cr.Append((byte)(authority.Name.Length), Encoding.ASCII.GetBytes(authority.Name));

            this.authorityName = authority.Name;

            // CA Index
            //co.KeyIndex = authority.KeyIndex;
            this.caId = authority.Id;
            cr.Append(caId);


            // public key
            rsa = RSA.Create();// new RSACryptoServiceProvider(2048);
            rsa.KeySize = 2048;
            RSAParameters dRSAKey = rsa.ExportParameters(true);
            cr.Append((byte)dRSAKey.Exponent.Length, dRSAKey.Exponent, (ushort)dRSAKey.Modulus.Length, dRSAKey.Modulus, AsymetricEncryptionAlgorithmType.RSA);

           
            publicRawData = cr.ToArray();

            // private key
            this.privateRawData = DC.Merge(dRSAKey.D, dRSAKey.DP, dRSAKey.DQ, dRSAKey.InverseQ, dRSAKey.P, dRSAKey.Q);

            this.signature = authority.Sign(publicRawData);

        }

        public override bool Save(string filename, bool includePrivate = false)
        {
            try
            {
                if (includePrivate)
                    File.WriteAllBytes(filename, BinaryList.ToBytes((byte)CertificateType.DomainPrivate, publicRawData, signature, privateRawData));
                else
                    File.WriteAllBytes(filename, BinaryList.ToBytes((byte)CertificateType.DomainPublic, publicRawData, signature));

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
                return BinaryList.ToBytes(publicRawData, signature, privateRawData);
            else
                return BinaryList.ToBytes(publicRawData, signature);
        }

    }
}
