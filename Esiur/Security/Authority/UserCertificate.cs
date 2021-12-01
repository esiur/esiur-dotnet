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

public class UserCertificate : Certificate
{
    uint ip;
    byte[] ip6;
    byte[] signature;
    string domain;
    string username;
    ulong domainId;

    public ulong DomainId
    {
        get { return domainId; }
    }

    public string Username
    {
        get { return username; }
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

    public UserCertificate(byte[] data, uint offset, uint length, bool privateKeyIncluded = false)
        : base(0, DateTime.MinValue, DateTime.MinValue, HashFunctionType.MD5)
    {
        var oOffset = offset;

        this.id = DC.GetUInt64(data, offset);
        offset += 8;

        // load IPs
        this.ip = DC.GetUInt32(data, offset);
        offset += 4;
        ip6 = DC.Clip(data, offset, 16);
        offset += 16;

        this.issueDate = DC.GetDateTime(data, offset);
        offset += 8;
        this.expireDate = DC.GetDateTime(data, offset);
        offset += 8;

        this.domainId = DC.GetUInt64(data, offset);
        offset += 8;

        this.domain = Encoding.ASCII.GetString(data, (int)offset + 1, data[offset]);
        offset += (uint)data[offset] + 1;

        this.username = Encoding.ASCII.GetString(data, (int)offset + 1, data[offset]);
        offset += (uint)data[offset] + 1;

        // Hash Function
        this.hashFunction = (HashFunctionType)(data[offset++] >> 4);

        // Public Key Encryption Algorithm
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

                this.privateRawData = DC.Clip(data, offset, privateKeyLength);

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

            this.signature = DC.Clip(data, offset, length - (offset - oOffset));
        }


    }

    public UserCertificate(ulong id, string username, DomainCertificate domainCertificate, DateTime issueDate,
                            DateTime expireDate, HashFunctionType hashFunction = HashFunctionType.SHA1, uint ip = 0, byte[] ip6 = null)
        : base(id, issueDate, expireDate, hashFunction)
    {
        // assign type
        var cr = new BinaryList();

        //id
        cr.AddUInt64(id);

        // ip
        this.ip = ip;
        this.ip6 = ip6;

        cr.AddUInt32(ip);


        if (ip6?.Length == 16)
            cr.AddUInt8Array(ip6);
        else
            cr.AddUInt8Array(new byte[16]);


        // dates
        this.issueDate = DateTime.UtcNow;
        this.expireDate = expireDate;

        cr.AddDateTime(issueDate)
          .AddDateTime(expireDate);


        // domain
        this.domainId = domainCertificate.Id;
        cr.AddUInt64(domainCertificate.Id);
        this.domain = domainCertificate.Domain;
        cr.AddUInt8((byte)domainCertificate.Domain.Length)
          .AddUInt8Array(Encoding.ASCII.GetBytes(domainCertificate.Domain));


        // username
        this.username = username;

        cr.AddUInt8((byte)(username.Length))
          .AddUInt8Array(Encoding.ASCII.GetBytes(username));

        // hash function (SHA1)
        cr.AddUInt8((byte)((byte)hashFunction << 4));// (byte)0x10);

        // public key

        rsa = RSA.Create();// new RSACryptoServiceProvider(2048);
        rsa.KeySize = 2048;
        // write public certificate file

        var key = rsa.ExportParameters(true);
        publicRawData = new BinaryList().AddUInt8((byte)key.Exponent.Length)
            .AddUInt8Array(key.Exponent)
            .AddUInt16((ushort)key.Modulus.Length)
            .AddUInt8Array(key.Modulus).ToArray();


        // sign it
        this.signature = domainCertificate.Sign(publicRawData);


        // store private info
        privateRawData = DC.Merge(key.D, key.DP, key.DQ, key.InverseQ, key.P, key.Q, signature);

    }

    public override bool Save(string filename, bool includePrivate = false)
    {
        try
        {
            if (includePrivate)
                File.WriteAllBytes(filename, DC.Merge(new byte[] { (byte)CertificateType.DomainPrivate }, publicRawData, signature, privateRawData));
            else
                File.WriteAllBytes(filename, DC.Merge(new byte[] { (byte)CertificateType.DomainPublic }, publicRawData, signature));

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
            return DC.Merge(publicRawData, signature, privateRawData);
        else
            return DC.Merge(publicRawData, signature);
    }
}
