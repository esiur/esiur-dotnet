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


        byte[] _localNonce, _remoteNonce;
        byte[] _localSalt, _remoteSalt;

        string _initiatorIdentity, _responderIdentity;

        byte[] _initiatorPassword, _responderPassword;

        string _hostName, _domain;

        int _step = 0;

        AuthenticationMode _mode;
        AuthenticationDirection _direction;

        PasswordAuthenticationProvider _provider;

        public IAuthenticationProvider Provider => _provider;


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

            if (_direction == AuthenticationDirection.Initiator)
            {
                if (_mode == AuthenticationMode.None)
                {
                    _step = -1;
                    return new AuthenticationResult(AuthenticationRuling.Failed, null);
                }
                else if (_mode == AuthenticationMode.InitializerIdentity)
                {
                    if (_step == 0)
                    {
                        // step 0: send local nonce and initiator identity.
                        if (_initiatorIdentity == null)
                            (_initiatorIdentity, _initiatorPassword) = _provider.GetSelfIdentityAndCredential(_domain, _hostName);
                        else
                            _initiatorPassword = _provider.GetSelfCredential(_initiatorIdentity, _domain, _hostName);

                        if (_initiatorPassword == null || _initiatorIdentity == null)
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);

                        // send local nonce and initiator identity
                        localAuthData.Add(_localNonce);
                        localAuthData.Add(_initiatorIdentity);

                        return new AuthenticationResult(AuthenticationRuling.InProgress, localAuthData);

                    }
                    else if (_step == 1)
                    {
                        // expect remote nonce, salt and challenge.
                        _remoteNonce = (byte[])remoteAuthData[0];
                        _remoteSalt = (byte[])remoteAuthData[1];
                        var remoteChallenge = (byte[])remoteAuthData[2];

                        // prevent reply attack by checking if remote nonce is same as local nonce.
                        if (_remoteNonce.SequenceEqual(_localNonce))
                        {
                            _step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // make salted hash of password.
                        var hashedPassword = ComputeSha3(_initiatorPassword.Concat(_remoteSalt).ToArray());


                        var expectedRemoteChallenge = ComputeSha3(_remoteNonce.Concat(hashedPassword)
                                                           .Concat(_localNonce)
                                                           .ToArray());

                        // compare remote challenge
                        if (!remoteChallenge.SequenceEqual(expectedRemoteChallenge))
                        {
                            _step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // make hash challenge response.
                        var localChallenge = ComputeSha3(_localNonce.Concat(hashedPassword)
                                                           .Concat(_remoteNonce)
                                                           .ToArray());

                        localAuthData.Add(localChallenge);
                        _step = -1;

                        // derive a session key from nonces and password.
                        // initiator identity + initiator password + initiator nonce + responder nonce

                        var sessionKey = ComputeSha3(_initiatorIdentity.ToBytes()
                                                           .Concat(hashedPassword)
                                                           .Concat(_localNonce)
                                                           .Concat(_remoteNonce)
                                                           .ToArray(), 512);

                        return new AuthenticationResult(AuthenticationRuling.Succeeded, localAuthData, _initiatorIdentity, null, sessionKey);
                    }
                    else
                    {
                        return new AuthenticationResult(AuthenticationRuling.Failed, null);
                    }
                }
                else if (_mode == AuthenticationMode.ResponderIdentity)
                {
                    if (_step == 0)
                    {
                        // just send local nonce.
                        localAuthData.Add(_localNonce);
                        return new AuthenticationResult(AuthenticationRuling.InProgress, localAuthData);
                    }
                    else if (_step == 1)
                    {
                        // expect responder identity and nonce.
                        _remoteNonce = (byte[])remoteAuthData[0];
                        _responderIdentity = (string)remoteAuthData[1];

                        // prevent reply attack by checking if remote nonce is same as local nonce.
                        if (_remoteNonce.SequenceEqual(_localNonce))
                        {
                            _step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // check if responder identity is valid and get password.
                        (_localSalt, _responderPassword) = _provider.GetHostedAccountCredential(_responderIdentity, _domain);

                        if (_responderPassword == null)
                        {
                            _step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // make hash challenge response.
                        var localChallenge = ComputeSha3(_localNonce.Concat(_responderPassword)
                                                           .Concat(_remoteNonce)
                                                           .ToArray());
                        // send localSalt and challenge
                        localAuthData.Add(_localSalt);
                        localAuthData.Add(localChallenge);
                        _step = 2;

                        return new AuthenticationResult(AuthenticationRuling.InProgress, localAuthData);

                    }
                    else if (_step == 2)
                    {
                        // expect remote challenge.
                        var remoteChallenge = (byte[])remoteAuthData[0];

                        // compare remote challenge

                        var expectedRemoteChallenge = ComputeSha3(_remoteNonce.Concat(_responderPassword)
                                                   .Concat(_localNonce)
                                                   .ToArray());

                        if (!remoteChallenge.SequenceEqual(expectedRemoteChallenge))
                        {
                            _step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // derive a session key from nonces and password.
                        // responder identity + responder hashed password + initiator nonce + responder nonce

                        var sessionKey = ComputeSha3(_responderIdentity.ToBytes()
                                                           .Concat(_responderPassword)
                                                           .Concat(_localNonce)
                                                           .Concat(_remoteNonce)
                                                           .ToArray(), 512);

                        _step = -1;
                        return new AuthenticationResult(AuthenticationRuling.Succeeded, null, _initiatorIdentity, _responderIdentity, sessionKey);

                    }
                    else
                    {
                        return new AuthenticationResult(AuthenticationRuling.Failed, null);
                    }
                }
                else if (_mode == AuthenticationMode.DualIdentity)
                {
                    if (_step == 0)
                    {
                        // step 0: send local nonce and initiator identity.
                        if (_initiatorIdentity == null)
                            (_initiatorIdentity, _initiatorPassword) = _provider.GetSelfIdentityAndCredential(_domain, _hostName);
                        else
                            _initiatorPassword = _provider.GetSelfCredential(_initiatorIdentity, _domain, _hostName);

                        if (_initiatorPassword == null || _initiatorIdentity == null)
                        {
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        localAuthData.Add(_localNonce);
                        localAuthData.Add(_initiatorIdentity);
                        return new AuthenticationResult(AuthenticationRuling.InProgress, localAuthData);

                    }
                    else if (_step == 1)
                    {
                        // expect responder identity, nonce and salt.
                        _remoteNonce = (byte[])remoteAuthData[0];
                        _responderIdentity = (string)remoteAuthData[1];
                        _remoteSalt = (byte[])remoteAuthData[2];

                        // prevent reply attack by checking if remote nonce is same as local nonce.
                        if (_remoteNonce.SequenceEqual(_localNonce))
                        {
                            _step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // check if responder identity is valid and get password.
                        (_localSalt, _responderPassword) = _provider.GetHostedAccountCredential(_responderIdentity, _domain);

                        if (_responderPassword == null)
                        {
                            _step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // make salted hash of password.
                        var hashedPassword = ComputeSha3(_initiatorPassword.Concat(_remoteSalt).ToArray());

                        // make hash challenge response.
                        var localChallenge = ComputeSha3(_localNonce.Concat(hashedPassword)
                                                           .Concat(_responderPassword)
                                                           .Concat(_remoteNonce)
                                                           .ToArray());

                        // send localSalt and challenge
                        localAuthData.Add(_localSalt);
                        localAuthData.Add(localChallenge);
                        _step = 2;

                        return new AuthenticationResult(AuthenticationRuling.InProgress, localAuthData);

                    }
                    else if (_step == 2)
                    {
                        // expect remote challenge.
                        var remoteChallenge = (byte[])remoteAuthData[0];

                        // make salted hash of password.
                        var hashedPassword = ComputeSha3(_initiatorPassword.Concat(_remoteSalt).ToArray());

                        // compare remote challenge
                        var expectedRemoteChallenge = ComputeSha3(_remoteNonce.Concat(hashedPassword)
                                                           .Concat(_responderPassword)
                                                           .Concat(_localNonce)
                                                           .ToArray());

                        if (!remoteChallenge.SequenceEqual(expectedRemoteChallenge))
                        {
                            _step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // derive a session key from nonces and password.
                        // responder identity + responder password + initiator nonce + responder nonce

                        var sessionKey = ComputeSha3(_initiatorIdentity.ToBytes()
                                                           .Concat(_responderIdentity.ToBytes())
                                                           .Concat(hashedPassword)
                                                           .Concat(_responderPassword)
                                                           .Concat(_localNonce)
                                                           .Concat(_remoteNonce)
                                                           .ToArray(), 512);

                        _step = -1;
                        return new AuthenticationResult(AuthenticationRuling.Succeeded, null, _initiatorIdentity, _responderIdentity, sessionKey);

                    }
                    else
                    {
                        return new AuthenticationResult(AuthenticationRuling.Failed, null);
                    }

                }
            }
            else if (_direction == AuthenticationDirection.Responder)
            {
                if (_mode == AuthenticationMode.None)
                {
                    _step = -1;
                    return new AuthenticationResult(AuthenticationRuling.Failed, null);
                }
                else if (_mode == AuthenticationMode.InitializerIdentity)
                {
                    if (_step == 0)
                    {
                        if (remoteAuthData.Length < 2)
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);

                        // step 0: expect remote nonce and initiator identity.
                        _remoteNonce = (byte[])remoteAuthData[0];
                        _initiatorIdentity = (string)remoteAuthData[1];

                        // prevent reply attack by checking if remote nonce is same as local nonce.
                        // @TODO: We can change our localNonce then send it
                        if (_remoteNonce.SequenceEqual(_localNonce))
                        {
                            _step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // get initiator password from provider.
                        (_localSalt, _initiatorPassword) = _provider.GetHostedAccountCredential(_initiatorIdentity, _domain);

                        // account not found or no password for this account.
                        if (_initiatorPassword == null || _initiatorIdentity == null)
                        {
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        var localChallenge = ComputeSha3(_localNonce.Concat(_initiatorPassword)
                                                               .Concat(_remoteNonce)
                                                               .ToArray());

                        // send local nonce, salt and challenge.
                        localAuthData.Add(_localNonce);
                        localAuthData.Add(_localSalt);
                        localAuthData.Add(localChallenge);

                        _step = 1;
                        return new AuthenticationResult(AuthenticationRuling.InProgress,
                                localAuthData);
                    }
                    else if (_step == 1)
                    {
                        // expect challenge response.
                        var remoteChallenge = (byte[])remoteAuthData[0];

                        var expectedRemoteChallenge = ComputeSha3(_remoteNonce.Concat(_initiatorPassword)
                                                                             .Concat(_localNonce)
                                                                             .ToArray());
                        // compare remote challenge
                        if (!expectedRemoteChallenge.SequenceEqual(remoteChallenge))
                        {
                            _step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // compute session key.

                        // derive a session key from nonces and password.
                        // initiator identity + initiator password + initiator nonce + responder nonce

                        var sessionKey = ComputeSha3(_initiatorIdentity.ToBytes()
                                                           .Concat(_initiatorPassword)
                                                           .Concat(_remoteNonce)
                                                           .Concat(_localNonce)
                                                           .ToArray(), 512);

                        _step = -1;
                        return new AuthenticationResult(AuthenticationRuling.Succeeded, null, _initiatorIdentity, _responderIdentity, sessionKey);

                    }
                    else
                    {
                        return new AuthenticationResult(AuthenticationRuling.Failed, null);
                    }

                }
                else if (_mode == AuthenticationMode.ResponderIdentity)
                {
                    if (_step == 0)
                    {
                        if (remoteAuthData.Length < 1)
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);

                        // step 0: receive remote nonce.
                        _remoteNonce = (byte[])remoteAuthData[0];

                        // prevent reply attack by checking if remote nonce is same as local nonce.
                        // @TODO: We can change our localNonce then send it
                        if (_remoteNonce.SequenceEqual(_localNonce))
                        {
                            _step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // get responder identity from provider.
                        if (_responderIdentity == null)
                            (_responderIdentity, _responderPassword) = _provider.GetSelfIdentityAndCredential(_domain, _hostName);
                        else
                            _responderPassword = _provider.GetSelfCredential(_responderIdentity, _domain, _hostName);

                        if (_responderPassword == null || _responderIdentity == null)
                        {
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        localAuthData.Add(_localNonce);
                        localAuthData.Add(_responderIdentity);

                        _step = 1;
                        // send local nonce and identity.
                        return new AuthenticationResult(AuthenticationRuling.InProgress,
                                localAuthData
                            );
                    }
                    else if (_step == 1)
                    {
                        // expect remote salt and challenge.
                        _remoteSalt = (byte[])remoteAuthData[0];
                        var remoteChallenge = (byte[])remoteAuthData[1];

                        // compute expected challenge response.
                        var hashedPassword = ComputeSha3(_responderPassword.Concat(_remoteSalt).ToArray());

                        var expectedRemoteChallenge = ComputeSha3(_remoteNonce.Concat(hashedPassword)
                                                   .Concat(_localNonce)
                                                   .ToArray());

                        // compare remote challenge
                        if (!expectedRemoteChallenge.SequenceEqual(remoteChallenge))
                        {
                            _step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // compute our challenge response.
                        var localChallenge = ComputeSha3(_localNonce.Concat(hashedPassword)
                                                           .Concat(_remoteNonce)
                                                           .ToArray());

                        // derive a session key from nonces and password.
                        // responder identity + responder hashed password + initiator nonce + responder nonce

                        var sessionKey = ComputeSha3(_responderIdentity.ToBytes()
                                                           .Concat(hashedPassword)
                                                           .Concat(_remoteNonce)
                                                           .Concat(_localNonce)
                                                           .ToArray(), 512);

                        localAuthData.Add(localChallenge);

                        _step = -1;
                        return new AuthenticationResult(AuthenticationRuling.Succeeded, localAuthData, _responderIdentity, null, sessionKey);
                    }
                    else
                    {
                        return new AuthenticationResult(AuthenticationRuling.Failed, null);
                    }

                }
                else if (_mode == AuthenticationMode.DualIdentity)
                {
                    if (_step == 0)
                    {
                        if (remoteAuthData.Length < 2)
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);

                        // step 0: receive remote nonce and initiator identity.
                        _remoteNonce = (byte[])remoteAuthData[0];
                        _initiatorIdentity = (string)remoteAuthData[1];

                        // prevent reply attack by checking if remote nonce is same as local nonce.
                        // @TODO: We can change our localNonce then send it
                        if (_remoteNonce.SequenceEqual(_localNonce))
                        {
                            _step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // get responder identity from provider.
                        if (_responderIdentity == null)
                            (_responderIdentity, _responderPassword) = _provider.GetSelfIdentityAndCredential(_domain, _hostName);
                        else
                            _responderPassword = _provider.GetSelfCredential(_responderIdentity, _domain, _hostName);

                        if (_responderPassword == null || _responderIdentity == null)
                        {
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // get initiator password from provider.
                        (_localSalt, _initiatorPassword) = _provider.GetHostedAccountCredential(_initiatorIdentity, _domain);

                        // account not found or no password for this account.
                        if (_initiatorPassword == null || _initiatorIdentity == null)
                        {
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // send local nonce, salt and responder identity.
                        localAuthData.Add(_localNonce);
                        localAuthData.Add(_localSalt);
                        localAuthData.Add(_responderIdentity);

                        _step = 1;
                        // send local nonce and identity.
                        return new AuthenticationResult(AuthenticationRuling.InProgress,
                                localAuthData
                            );

                    }
                    else if (_step == 1)
                    {
                        // expect initiator salt and challenge.
                        var remoteSalt = (byte[])remoteAuthData[0];
                        var remoteChallenge = (byte[])remoteAuthData[1];

                        // compute expected challenge response.
                        var hashedPassword = ComputeSha3(_responderPassword.Concat(remoteSalt).ToArray());

                        // compare remote challenge
                        var expectedRemoteChallenge = ComputeSha3(_remoteNonce.Concat(_initiatorPassword)
                                                           .Concat(hashedPassword)
                                                           .Concat(_localNonce)
                                                           .ToArray());

                        // compare remote challenge
                        if (!expectedRemoteChallenge.SequenceEqual(remoteChallenge))
                        {
                            _step = -1;
                            return new AuthenticationResult(AuthenticationRuling.Failed, null);
                        }

                        // compute our challenge
                        var localChallenge = ComputeSha3(_localNonce.Concat(hashedPassword)
                                                           .Concat(_initiatorPassword)
                                                           .Concat(_remoteNonce)
                                                           .ToArray());

                        localAuthData.Add(localChallenge);

                        // derive a session key from nonces and password.
                        // responder identity + responder password + initiator nonce + responder nonce

                        var sessionKey = ComputeSha3(_initiatorIdentity.ToBytes()
                                                           .Concat(_responderIdentity.ToBytes())
                                                           .Concat(_initiatorPassword)
                                                           .Concat(hashedPassword)
                                                           .Concat(_remoteNonce)
                                                           .Concat(_localNonce)
                                                           .ToArray(), 512);

                        _step = -1;
                        return new AuthenticationResult(AuthenticationRuling.Succeeded, localAuthData, _initiatorIdentity, _responderIdentity, sessionKey);

                    }
                    else
                    {
                        return new AuthenticationResult(AuthenticationRuling.Failed, null);
                    }
                }
            }


            _step = -1;
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
            _localNonce = Global.GenerateBytes(20);

            this._provider = provider;
            this._initiatorIdentity = initiatorIdentity;
            this._responderIdentity = responderIdentity;
            this._mode = mode;
            this._direction = direction;
            this._domain = domain;
            this._hostName = hostName;
        }
    }
}
