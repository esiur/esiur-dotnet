using Esiur.Core;
using Esiur.Data;
using Esiur.Net.Packets;
using Esiur.Resource;
using Esiur.Security.Authority;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Esiur.Security.Membership
{
    public class SimpleMembership : IMembership
    {
        public bool GuestsAllowed { get; set; } = false;

        public event ResourceEventHandler<AuthorizationIndication> Authorization;

        KeyList<string, UserInfo> users = new KeyList<string, UserInfo>();

        KeyList<ulong, TokenInfo> tokens = new KeyList<ulong, TokenInfo>();

        public class QuestionAnswer
        {
            public string Question { get; set; }
            public object Answer { get; set; }
            public bool Hashed { get; set; }
        }

        public class UserInfo
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public QuestionAnswer[] Questions { get; set; }

            public List<AuthorizationResults> Results { get; set; } = new List<AuthorizationResults>();
        }

        public class TokenInfo
        {
            public ulong Index { get; set; }
            public string Token { get; set; }
            public string Username { get; set; }
        }

        public void AddToken(ulong index, string value, string username)
        {
            if (users.ContainsKey(username))
                throw new Exception("User not found.");

            tokens.Add(index, new TokenInfo() { Index = index, Token = value, Username = username });
        }

        public void AddUser(string username, string password, QuestionAnswer[] questions)
        {
            users.Add(username, new UserInfo() { Password = password, Username = username, Questions = questions });
        }

        public void RemoveToken(ulong index) => tokens.Remove(index);

        public void RemoveUser(string username) => users.Remove(username);



        public AsyncReply<AuthorizationResults> Authorize(Session session)
        {
            if (session.AuthorizedAccount.StartsWith("g-"))
                return new AsyncReply<AuthorizationResults>(new AuthorizationResults() { Response = AuthorizationResultsResponse.Success });

            if (users[session.AuthorizedAccount].Questions.Length > 0)
            {
                var q = users[session.AuthorizedAccount].Questions.First();

                var r = new Random();

                var format = q.Answer.GetIAuthFormat();

                var ar = new AuthorizationResults()
                {
                    Clue = q.Question,
                    Destination = IIPAuthPacketIAuthDestination.Self,
                    Reference = (uint)r.Next(),
                    RequiredFormat = format,
                    Expire = DateTime.Now.AddSeconds(60),
                    Response = q.Hashed ? AuthorizationResultsResponse.IAuthHashed : AuthorizationResultsResponse.IAuthPlain
                };

                users[session.AuthorizedAccount].Results.Add(ar);

                return new AsyncReply<AuthorizationResults>(ar);
            }
            else
            {
                return new AsyncReply<AuthorizationResults>(new AuthorizationResults() { Response = AuthorizationResultsResponse.Success });
            }
        }

        public AsyncReply<AuthorizationResults> AuthorizeEncrypted(Session session, uint reference, IIPAuthPacketPublicKeyAlgorithm algorithm, byte[] value)
        {
            throw new NotImplementedException();
        }

        public AsyncReply<AuthorizationResults> AuthorizeHashed(Session session, uint reference, IIPAuthPacketHashAlgorithm algorithm, byte[] value)
        {
            if (algorithm != IIPAuthPacketHashAlgorithm.SHA256)
                throw new NotImplementedException();

            var ar = users[session.AuthorizedAccount].Results.First(x => x.Reference == reference);

            var qa = users[session.AuthorizedAccount].Questions.First(x => x.Question == ar.Clue);


            // compute hash
            var remoteNonce = (byte[])session.RemoteHeaders[IIPAuthPacketHeader.Nonce];
            var localNonce = (byte[])session.LocalHeaders[IIPAuthPacketHeader.Nonce];

            var hashFunc = SHA256.Create();
            // local nonce + password or token + remote nonce
            var challenge = hashFunc.ComputeHash(new BinaryList()
                                                .AddUInt8Array(remoteNonce)
                                                .AddUInt8Array(Codec.Compose(qa.Answer, null))
                                                .AddUInt8Array(localNonce)
                                                .ToArray());

            if (challenge.SequenceEqual(value))
                return new AsyncReply<AuthorizationResults>(new AuthorizationResults() { Response = AuthorizationResultsResponse.Success });
            else
                return new AsyncReply<AuthorizationResults>(new AuthorizationResults() { Response = AuthorizationResultsResponse.Failed });

        }

        public AsyncReply<AuthorizationResults> AuthorizePlain(Session session, uint reference, object value)
        {
            var ar = users[session.AuthorizedAccount].Results.First(x => x.Reference == reference);

            var qa = users[session.AuthorizedAccount].Questions.First(x => x.Question == ar.Clue);

            
            if (qa.Answer.ToString() == value.ToString())
                return new AsyncReply<AuthorizationResults>(new AuthorizationResults() { Response = AuthorizationResultsResponse.Success });
            else
                return new AsyncReply<AuthorizationResults>(new AuthorizationResults() { Response = AuthorizationResultsResponse.Failed });

        }

        public AsyncReply<byte[]> GetPassword(string username, string domain)
        {
            return new AsyncReply<byte[]>(DC.ToBytes(users[username].Password));
        }

        public AsyncReply<byte[]> GetToken(ulong tokenIndex, string domain)
        {
            return new AsyncReply<byte[]>(DC.ToBytes(tokens[tokenIndex].Token));
        }

        public AsyncReply<bool> Login(Session session)
        {
            return new AsyncReply<bool>(true);
        }

        public AsyncReply<bool> Logout(Session session)
        {
            return new AsyncReply<bool>(true);
        }

        public AsyncReply<string> TokenExists(ulong tokenIndex, string domain)
        {
            if (!tokens.ContainsKey(tokenIndex))
                return new AsyncReply<string>(null);
            else
                return new AsyncReply<string>(tokens[tokenIndex].Username);
        }

        public AsyncReply<string> UserExists(string username, string domain)
        {
            if (!users.ContainsKey(username))
                return new AsyncReply<string>(null);
            else
                return new AsyncReply<string>(username);
        }

    }
}
