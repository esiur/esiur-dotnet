using Esiur.Misc;
using Esiur.Security.Permissions;
using Org.BouncyCastle.Crypto.Digests;
using System;
using System.Collections.Generic;
using System.Text;
using Esiur.Data;
using Esiur.Data.Types;

namespace Esiur.Security.Authority.Providers
{
    internal class PasswordAuthenticationHandler : IAuthenticationHandler
    {
        public string Protocol => "hash";

        byte[] localNonce, remoteNonce;
        byte[] localSalt, remoteSalt;

        string initiatorIdentity, responderIdentity;

        byte[] initiatorPassword, responderPassword;

        string hostName, domain;

        int step = 0;

        AuthenticationMode mode;
        AuthenticationDirection direction;

        PasswordAuthenticationProvider provider;


        public byte[] ComputeSha3(byte[] data, int bitLength = 256)
        {
            // 1. Initialize the digest (supports 224, 256, 384, 512)
            var digest = new Sha3Digest(bitLength);

            // 3. Update the digest with data
            digest.BlockUpdate(data, 0, data.Length);

            // 4. Retrieve the final hash
            byte[] result = new byte[digest.GetDigestSize()];
            digest.DoFinal(result, 0);

            return result;
        }

        public AuthenticationResult Process(object authData)
        {
            var remoteAuthData = (object[])authData;
            var localAuthData = new List<object>();

            if (direction == AuthenticationDirection.Initiator)
            {
                if (mode == AuthenticationMode.None)
                {
                    step = -1;
                    return new AuthenticationResult(AuthenticationRuling.Failed, null);
                }
                else if (mode == AuthenticationMode.InitializerIdentity)
                {
                    if (step == 0)
                    {
                        // step 0: send local nonce and initiator identity.
                        if (initiatorIdentity == null)
                            (initiatorIdentity, initiatorPassword) = provider.GetSelfIdentityAndCredential(domain, hostName);
                        else
                            initiatorPassword = provider.GetSelfCredential(initiatorIdentity, domain, hostName);

                        if (initiatorPassword == null || initiatorIdentity == null)
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);

                        // send local nonce and initiator identity
                        localAuthData.Add(localNonce);
                        localAuthData.Add(initiatorIdentity);

                        return new AuthenticationResult(AuthenticationRuling.InProgress, localAuthData);

                    }
                    else if (step == 1)
                    {
                        // expect remote nonce, salt and challenge.
                        remoteNonce = (byte[])remoteAuthData[0];
                        remoteSalt = (byte[])remoteAuthData[1];
                        var remoteChallenge = (byte[])remoteAuthData[2];

                        // prevent reply attack by checking if remote nonce is same as local nonce.
                        if (remoteNonce.SequenceEqual(localNonce))
                        {
                            step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // make salted hash of password.
                        var hashedPassword = ComputeSha3(initiatorPassword.Concat(remoteSalt).ToArray());


                        var expectedRemoteChallenge = ComputeSha3(remoteNonce.Concat(hashedPassword)
                                                           .Concat(localNonce)
                                                           .ToArray());

                        // compare remote challenge
                        if (!remoteChallenge.SequenceEqual(expectedRemoteChallenge))
                        {
                            step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // make hash challenge response.
                        var localChallenge = ComputeSha3(localNonce.Concat(hashedPassword)
                                                           .Concat(remoteNonce)
                                                           .ToArray());

                        localAuthData.Add(localChallenge);
                        step = -1;

                        // derive a session key from nonces and password.
                        // initiator identity + initiator password + initiator nonce + responder nonce

                        var sessionKey = ComputeSha3(initiatorIdentity.ToBytes()
                                                           .Concat(hashedPassword)
                                                           .Concat(localNonce)
                                                           .Concat(remoteNonce)
                                                           .ToArray(), 512);

                        return new AuthenticationResult(AuthenticationRuling.Succeeded, localAuthData, initiatorIdentity, null, sessionKey);
                    }
                    else
                    {
                        return new AuthenticationResult(AuthenticationRuling.Failed, null);
                    }
                }
                else if (mode == AuthenticationMode.ResponderIdentity)
                {
                    if (step == 0)
                    {
                        // just send local nonce.
                        localAuthData.Add(localNonce);
                        return new AuthenticationResult(AuthenticationRuling.InProgress, localAuthData);
                    }
                    else if (step == 1)
                    {
                        // expect responder identity and nonce.
                        remoteNonce = (byte[])remoteAuthData[0];
                        responderIdentity = (string)remoteAuthData[1];

                        // prevent reply attack by checking if remote nonce is same as local nonce.
                        if (remoteNonce.SequenceEqual(localNonce))
                        {
                            step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // check if responder identity is valid and get password.
                        (localSalt, responderPassword) = provider.GetHostedAccountCredential(responderIdentity, domain);

                        if (responderPassword == null)
                        {
                            step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // make hash challenge response.
                        var localChallenge = ComputeSha3(localNonce.Concat(responderPassword)
                                                           .Concat(remoteNonce)
                                                           .ToArray());
                        // send localSalt and challenge
                        localAuthData.Add(localSalt);
                        localAuthData.Add(localChallenge);
                        step = 2;

                        return new AuthenticationResult(AuthenticationRuling.InProgress, localAuthData);

                    }
                    else if (step == 2)
                    {
                        // expect remote challenge.
                        var remoteChallenge = (byte[])remoteAuthData[0];

                        // compare remote challenge

                        var expectedRemoteChallenge = ComputeSha3(remoteNonce.Concat(responderPassword)
                                                   .Concat(localNonce)
                                                   .ToArray());

                        if (!remoteChallenge.SequenceEqual(expectedRemoteChallenge))
                        {
                            step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // derive a session key from nonces and password.
                        // responder identity + responder hashed password + initiator nonce + responder nonce

                        var sessionKey = ComputeSha3(responderIdentity.ToBytes()
                                                           .Concat(responderPassword)
                                                           .Concat(localNonce)
                                                           .Concat(remoteNonce)
                                                           .ToArray(), 512);

                        step = -1;
                        return new AuthenticationResult(AuthenticationRuling.Succeeded, null, initiatorIdentity, responderIdentity, sessionKey);

                    }
                    else
                    {
                        return new AuthenticationResult(AuthenticationRuling.Failed, null);
                    }
                }
                else if (mode == AuthenticationMode.DualIdentity)
                {
                    if (step == 0)
                    {
                        // step 0: send local nonce and initiator identity.
                        if (initiatorIdentity == null)
                            (initiatorIdentity, initiatorPassword) = provider.GetSelfIdentityAndCredential(domain, hostName);
                        else
                            initiatorPassword = provider.GetSelfCredential(initiatorIdentity, domain, hostName);

                        if (initiatorPassword == null || initiatorIdentity == null)
                        {
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        localAuthData.Add(localNonce);
                        localAuthData.Add(initiatorIdentity);
                        return new AuthenticationResult(AuthenticationRuling.InProgress, localAuthData);

                    }
                    else if (step == 1)
                    {
                        // expect responder identity, nonce and salt.
                        remoteNonce = (byte[])remoteAuthData[0];
                        responderIdentity = (string)remoteAuthData[1];
                        remoteSalt = (byte[])remoteAuthData[2];

                        // prevent reply attack by checking if remote nonce is same as local nonce.
                        if (remoteNonce.SequenceEqual(localNonce))
                        {
                            step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // check if responder identity is valid and get password.
                        (localSalt, responderPassword) = provider.GetHostedAccountCredential(responderIdentity, domain);

                        if (responderPassword == null)
                        {
                            step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // make salted hash of password.
                        var hashedPassword = ComputeSha3(initiatorPassword.Concat(remoteSalt).ToArray());

                        // make hash challenge response.
                        var localChallenge = ComputeSha3(localNonce.Concat(hashedPassword)
                                                           .Concat(responderPassword)
                                                           .Concat(remoteNonce)
                                                           .ToArray());

                        // send localSalt and challenge
                        localAuthData.Add(localSalt);
                        localAuthData.Add(localChallenge);
                        step = 2;

                        return new AuthenticationResult(AuthenticationRuling.InProgress, localAuthData);

                    }
                    else if (step == 2)
                    {
                        // expect remote challenge.
                        var remoteChallenge = (byte[])remoteAuthData[0];

                        // make salted hash of password.
                        var hashedPassword = ComputeSha3(initiatorPassword.Concat(remoteSalt).ToArray());

                        // compare remote challenge
                        var expectedRemoteChallenge = ComputeSha3(remoteNonce.Concat(hashedPassword)
                                                           .Concat(responderPassword)
                                                           .Concat(localNonce)
                                                           .ToArray());

                        if (!remoteChallenge.SequenceEqual(expectedRemoteChallenge))
                        {
                            step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // derive a session key from nonces and password.
                        // responder identity + responder password + initiator nonce + responder nonce

                        var sessionKey = ComputeSha3(initiatorIdentity.ToBytes()
                                                           .Concat(responderIdentity.ToBytes())
                                                           .Concat(hashedPassword)
                                                           .Concat(responderPassword)
                                                           .Concat(localNonce)
                                                           .Concat(remoteNonce)
                                                           .ToArray(), 512);

                        step = -1;
                        return new AuthenticationResult(AuthenticationRuling.Succeeded, null, initiatorIdentity, responderIdentity, sessionKey);

                    }
                    else
                    {
                        return new AuthenticationResult(AuthenticationRuling.Failed, null);
                    }

                }
            }
            else if (direction == AuthenticationDirection.Responder)
            {
                if (mode == AuthenticationMode.None)
                {
                    step = -1;
                    return new AuthenticationResult(AuthenticationRuling.Failed, null);
                }
                else if (mode == AuthenticationMode.InitializerIdentity)
                {
                    if (step == 0)
                    {
                        if (remoteAuthData.Length < 2)
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);

                        // step 0: expect remote nonce and initiator identity.
                        remoteNonce = (byte[])remoteAuthData[0];
                        initiatorIdentity = (string)remoteAuthData[1];

                        // prevent reply attack by checking if remote nonce is same as local nonce.
                        // @TODO: We can change our localNonce then send it
                        if (remoteNonce.SequenceEqual(localNonce))
                        {
                            step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // get initiator password from provider.
                        (localSalt, initiatorPassword) = provider.GetHostedAccountCredential(initiatorIdentity, domain);

                        // account not found or no password for this account.
                        if (initiatorPassword == null || initiatorIdentity == null)
                        {
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        var localChallenge = ComputeSha3(localNonce.Concat(initiatorPassword)
                                                               .Concat(remoteNonce)
                                                               .ToArray());

                        // send local nonce, salt and challenge.
                        localAuthData.Add(localNonce);
                        localAuthData.Add(localSalt);
                        localAuthData.Add(localChallenge);

                        step = 1;
                        return new AuthenticationResult(AuthenticationRuling.InProgress,
                                localAuthData);
                    }
                    else if (step == 1)
                    {
                        // expect challenge response.
                        var remoteChallenge = (byte[])remoteAuthData[0];

                        var expectedRemoteChallenge = ComputeSha3(remoteNonce.Concat(initiatorPassword)
                                                                             .Concat(localNonce)
                                                                             .ToArray());
                        // compare remote challenge
                        if (!expectedRemoteChallenge.SequenceEqual(remoteChallenge))
                        {
                            step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // compute session key.

                        // derive a session key from nonces and password.
                        // initiator identity + initiator password + initiator nonce + responder nonce

                        var sessionKey = ComputeSha3(initiatorIdentity.ToBytes()
                                                           .Concat(initiatorPassword)
                                                           .Concat(remoteNonce)
                                                           .Concat(localNonce)
                                                           .ToArray(), 512);

                        step = -1;
                        return new AuthenticationResult(AuthenticationRuling.Succeeded, null, initiatorIdentity, responderIdentity, sessionKey);

                    }
                    else
                    {
                        return new AuthenticationResult(AuthenticationRuling.Failed, null);
                    }

                }
                else if (mode == AuthenticationMode.ResponderIdentity)
                {
                    if (step == 0)
                    {
                        if (remoteAuthData.Length < 1)
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);

                        // step 0: receive remote nonce.
                        remoteNonce = (byte[])remoteAuthData[0];

                        // prevent reply attack by checking if remote nonce is same as local nonce.
                        // @TODO: We can change our localNonce then send it
                        if (remoteNonce.SequenceEqual(localNonce))
                        {
                            step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // get responder identity from provider.
                        if (responderIdentity == null)
                            (responderIdentity, responderPassword) = provider.GetSelfIdentityAndCredential(domain, hostName);
                        else
                            responderPassword = provider.GetSelfCredential(responderIdentity, domain, hostName);

                        if (responderPassword == null || responderIdentity == null)
                        {
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        localAuthData.Add(localNonce);
                        localAuthData.Add(responderIdentity);

                        step = 1;
                        // send local nonce and identity.
                        return new AuthenticationResult(AuthenticationRuling.InProgress,
                                localAuthData
                            );
                    }
                    else if (step == 1)
                    {
                        // expect remote salt and challenge.
                        remoteSalt = (byte[])remoteAuthData[0];
                        var remoteChallenge = (byte[])remoteAuthData[1];

                        // compute expected challenge response.
                        var hashedPassword = ComputeSha3(responderPassword.Concat(remoteSalt).ToArray());

                        var expectedRemoteChallenge = ComputeSha3(remoteNonce.Concat(hashedPassword)
                                                   .Concat(localNonce)
                                                   .ToArray());

                        // compare remote challenge
                        if (!expectedRemoteChallenge.SequenceEqual(remoteChallenge))
                        {
                            step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // compute our challenge response.
                        var localChallenge = ComputeSha3(localNonce.Concat(hashedPassword)
                                                           .Concat(remoteNonce)
                                                           .ToArray());

                        // derive a session key from nonces and password.
                        // responder identity + responder hashed password + initiator nonce + responder nonce

                        var sessionKey = ComputeSha3(responderIdentity.ToBytes()
                                                           .Concat(hashedPassword)
                                                           .Concat(remoteNonce)
                                                           .Concat(localNonce)
                                                           .ToArray(), 512);

                        localAuthData.Add(localChallenge);

                        step = -1;
                        return new AuthenticationResult(AuthenticationRuling.Succeeded, localAuthData, responderIdentity, null, sessionKey);
                    }
                    else
                    {
                        return new AuthenticationResult(AuthenticationRuling.Failed, null);
                    }

                }
                else if (mode == AuthenticationMode.DualIdentity)
                {
                    if (step == 0)
                    {
                        if (remoteAuthData.Length < 2)
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);

                        // step 0: receive remote nonce and initiator identity.
                        remoteNonce = (byte[])remoteAuthData[0];
                        initiatorIdentity = (string)remoteAuthData[1];

                        // prevent reply attack by checking if remote nonce is same as local nonce.
                        // @TODO: We can change our localNonce then send it
                        if (remoteNonce.SequenceEqual(localNonce))
                        {
                            step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // get responder identity from provider.
                        if (responderIdentity == null)
                            (responderIdentity, responderPassword) = provider.GetSelfIdentityAndCredential(domain, hostName);
                        else
                            responderPassword = provider.GetSelfCredential(responderIdentity, domain, hostName);

                        if (responderPassword == null || responderIdentity == null)
                        {
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // get initiator password from provider.
                        (localSalt, initiatorPassword) = provider.GetHostedAccountCredential(initiatorIdentity, domain);

                        // account not found or no password for this account.
                        if (initiatorPassword == null || initiatorIdentity == null)
                        {
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // send local nonce, salt and responder identity.
                        localAuthData.Add(localNonce);
                        localAuthData.Add(localSalt);
                        localAuthData.Add(responderIdentity);

                        step = 1;
                        // send local nonce and identity.
                        return new AuthenticationResult(AuthenticationRuling.InProgress,
                                localAuthData
                            );

                    }
                    else if (step == 1)
                    {
                        // expect initiator salt and challenge.
                        var remoteSalt = (byte[])remoteAuthData[0];
                        var remoteChallenge = (byte[])remoteAuthData[1];

                        // compute expected challenge response.
                        var hashedPassword = ComputeSha3(responderPassword.Concat(remoteSalt).ToArray());

                        // compare remote challenge
                        var expectedRemoteChallenge = ComputeSha3(remoteNonce.Concat(initiatorPassword)
                                                           .Concat(hashedPassword)
                                                           .Concat(localNonce)
                                                           .ToArray());

                        // compare remote challenge
                        if (!expectedRemoteChallenge.SequenceEqual(remoteChallenge))
                        {
                            step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // compute our challenge
                        var localChallenge = ComputeSha3(localNonce.Concat(hashedPassword)
                                                           .Concat(initiatorPassword)
                                                           .Concat(remoteNonce)
                                                           .ToArray());

                        localAuthData.Add(localChallenge);

                        // derive a session key from nonces and password.
                        // responder identity + responder password + initiator nonce + responder nonce

                        var sessionKey = ComputeSha3(initiatorIdentity.ToBytes()
                                                           .Concat(responderIdentity.ToBytes())
                                                           .Concat(initiatorPassword)
                                                           .Concat(hashedPassword)
                                                           .Concat(remoteNonce)
                                                           .Concat(localNonce)
                                                           .ToArray(), 512);

                        step = -1;
                        return new AuthenticationResult(AuthenticationRuling.Succeeded, localAuthData, initiatorIdentity, responderIdentity, sessionKey);

                    }
                    else
                    {
                        return new AuthenticationResult(AuthenticationRuling.Failed, null);
                    }
                }
            }


            step = -1;
            return new AuthenticationResult(AuthenticationRuling.Failed, null);
        }

        public PasswordAuthenticationHandler(AuthenticationMode mode,
            AuthenticationDirection direction,
            string initiatorIdentity,
            string responderIdentity,
            string hostName,
            string domain,
            PasswordAuthenticationProvider provider)
        {
            localNonce = Global.GenerateBytes(20);

            this.provider = provider;
            this.initiatorIdentity = initiatorIdentity;
            this.responderIdentity = responderIdentity;
            this.mode = mode;
            this.direction = direction;
            this.domain = domain;
            this.hostName = hostName;
        }
    }
}
