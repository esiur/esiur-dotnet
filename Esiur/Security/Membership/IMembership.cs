using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esiur.Data;
using Esiur.Net.IIP;
using Esiur.Engine;
using Esiur.Security.Authority;
using Esiur.Resource;

namespace Esiur.Security.Membership
{
    public interface IMembership:IResource
    {
        //IUser[] GetUsers(QueryFilter<string> user);

        //bool AddCertificate(Certificate certificate);

        //CACertificate[] GetCACertificates(string authority);
        //UserCertificate[] GetUserCertificate(string user, string domain);
        //DomainCertificate[] GetDomainCertificates(string domain);


        bool UserExists(string username);
        AsyncReply<byte[]> GetPassword(string username, string domain);

        //ClientAuthentication Authenticate(string username, byte[] credentials, int flag);
        //HostAuthentication Authenticate(DomainCertificate domainCertificate);
        //CoHostAuthentication Authenticate(DomainCertificate hostCertificate, int hostId);

        /*
        object GetUserInfo(User user, string field);
        object[] GetUserInfo(User user, string[] fields);

        bool SetUserInfo(User user, string field, object value);
        bool SetUserInfo(User user, KeyList<string, object> info);
        */

        //bool AddUser(User user, KeyList<string, object> info);
        //bool RemoveUser(string username);





    }
}
