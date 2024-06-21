using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets.HTTP
{
    public enum HTTPResponseCode : int
    {
        Switching = 101,
        OK = 200,
        Created = 201,
        Accepted = 202,
        NoContent = 204,
        MovedPermanently = 301,
        Found = 302,
        SeeOther = 303,
        NotModified = 304,
        TemporaryRedirect = 307,
        BadRequest = 400,
        Unauthorized = 401,
        Forbidden = 403,
        NotFound = 404,
        MethodNotAllowed = 405,
        NotAcceptable = 406,
        PreconditionFailed = 412,
        UnsupportedMediaType = 415,
        InternalServerError = 500,
        NotImplemented = 501,
    }
}
