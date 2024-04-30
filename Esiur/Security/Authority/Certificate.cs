/*
 
Copyright (c) 2019 - 2024 Ahmed Kh. Zamil

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

public abstract class Certificate
{
    protected DateTime issueDate, expireDate;
    protected RSA rsa;
    protected Aes aes;

    protected byte[] publicRawData;
    protected byte[] privateRawData;
    protected ulong id;
    protected HashFunctionType hashFunction;

    public Certificate(ulong id, DateTime issueDate, DateTime expireDate, HashFunctionType hashFunction)
    {
        this.id = id;
        this.issueDate = issueDate;
        this.expireDate = expireDate;
        this.hashFunction = hashFunction;
    }

    public ulong Id
    {
        get { return id; }
    }

    public AsymetricEncryptionAlgorithmType AsymetricEncryptionAlgorithm
    {
        get { return AsymetricEncryptionAlgorithmType.RSA; }
    }

    public byte[] AsymetricEncrypt(byte[] message)
    {
        return rsa.Encrypt(message, RSAEncryptionPadding.OaepSHA512);
    }


    public byte[] AsymetricEncrypt(byte[] message, uint offset, uint length)
    {
        if (message.Length != length)
            return rsa.Encrypt(DC.Clip(message, offset, length), RSAEncryptionPadding.OaepSHA512);
        else
            return rsa.Encrypt(message, RSAEncryptionPadding.OaepSHA512);
    }

    public byte[] AsymetricDecrypt(byte[] message)
    {
        try
        {
            return rsa.Decrypt(message, RSAEncryptionPadding.OaepSHA512);
        }
        catch (Exception ex)
        {
            Global.Log("Certificate", LogType.Error, ex.ToString());
            return null;
        }
    }

    public byte[] AsymetricDecrypt(byte[] message, uint offset, uint length)
    {
        try
        {
            if (message.Length != length)
                return rsa.Decrypt(DC.Clip(message, offset, length), RSAEncryptionPadding.OaepSHA512);
            else
                return rsa.Decrypt(message, RSAEncryptionPadding.OaepSHA512);

        }
        catch (Exception ex)
        {
            Global.Log("Certificate", LogType.Error, ex.ToString());
            return null;
        }
    }

    public byte[] SymetricEncrypt(byte[] message, uint offset, uint length)
    {
        byte[] rt = null;

        using (var ms = new MemoryStream())
        {
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                cs.Write(message, (int)offset, (int)length);

            rt = ms.ToArray();
        }

        return rt;
    }

    public byte[] SymetricEncrypt(byte[] message)
    {
        return SymetricEncrypt(message, 0, (uint)message.Length);
    }

    public byte[] SymetricDecrypt(byte[] message, uint offset, uint length)
    {
        byte[] rt = null;

        using (var ms = new MemoryStream())
        {
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                cs.Write(message, (int)offset, (int)length);

            rt = ms.ToArray();
        }

        return rt;
    }

    public byte[] SymetricDecrypt(byte[] message)
    {
        return SymetricDecrypt(message, 0, (uint)message.Length);
    }

    public byte[] Sign(byte[] message)
    {
        return Sign(message, 0, (uint)message.Length);
    }

    public byte[] Sign(byte[] message, uint offset, uint length)
    {
        if (hashFunction == HashFunctionType.SHA1)
            return rsa.SignData(message, (int)offset, (int)length, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
        else if (hashFunction == HashFunctionType.MD5)
            return rsa.SignData(message, (int)offset, (int)length, HashAlgorithmName.MD5, RSASignaturePadding.Pkcs1);
        else if (hashFunction == HashFunctionType.SHA256)
            return rsa.SignData(message, (int)offset, (int)length, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        else if (hashFunction == HashFunctionType.SHA384)
            return rsa.SignData(message, (int)offset, (int)length, HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1);
        else if (hashFunction == HashFunctionType.SHA512)
            return rsa.SignData(message, (int)offset, (int)length, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);

        return null;
    }

    public bool InitializeSymetricCipher(SymetricEncryptionAlgorithmType algorithm, int keyLength, byte[] key, byte[] iv)
    {
        if (algorithm == SymetricEncryptionAlgorithmType.AES)
        {
            if (keyLength == 0) // 128 bit
            {
                aes = Aes.Create();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                return true;
            }
        }

        return false;
    }


    public abstract bool Save(string filename, bool includePrivate = false);
    public abstract byte[] Serialize(bool includePrivate = false);

    public static Certificate Load(string filename)
    {
        byte[] ar = File.ReadAllBytes(filename);
        var t = (CertificateType)ar[0];

        switch (t)
        {
            case CertificateType.CAPublic:
                return new CACertificate(ar, 1, (uint)ar.Length - 1);
            case CertificateType.CAPrivate:
                return new CACertificate(ar, 1, (uint)ar.Length - 1, true);
            case CertificateType.DomainPublic:
                return new DomainCertificate(ar, 1, (uint)ar.Length - 1);
            case CertificateType.DomainPrivate:
                return new DomainCertificate(ar, 1, (uint)ar.Length - 1, true);
            case CertificateType.UserPublic:
                return new UserCertificate(ar, 1, (uint)ar.Length - 1);
            case CertificateType.UserPrivate:
                return new UserCertificate(ar, 1, (uint)ar.Length - 1, true);
        }

        return null;
    }
}

