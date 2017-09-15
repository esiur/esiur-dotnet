using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Security.Authority
{
    public enum SourceAttributeType
    {
        Mobility, // Stationary/Mobile
        CPU, // Arc, Speed, Cores
        IP, // IPv4, IPv6 Address
        Route, // Trace Root
        Location, // Lon, Lat, Alt, Accuracy
        OS, // OS name, version, distro, kernel
        Application, // lib version, app version
        Network, // Bandwidth, MAC, IP, Route
        Display, // Screen WxH
        Media, // AudioIn,  AudioOut, VideoIn,
        Identity, // IMEI, IMSI, Manufacture
    }
    /*
    public class SourceAttribute
    {
        SourceAttributeType type;
        Structure value;

        public SourceAttributeType Type
        {
            get
            {
                return type;
            }
        }

        public Structure Value
        {
            get
            {
                return value;
            }
        }

        public SourceAttribute(SourceAttributeType type, Structure value)
        {
            this.type = type;
            this.value = value;
        }
    }
    */
}
