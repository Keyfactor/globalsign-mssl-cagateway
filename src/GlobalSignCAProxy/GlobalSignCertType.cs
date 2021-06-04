using System.Collections.Generic;

namespace Keyfactor.Extensions.AnyGateway.GlobalSign
{
    public class GlobalSignCertType
    {
        public bool HasWildCard { get; private set; }
        public GSCertificateType Type { get; private set; }
        public string DisplayName { get; private set; }
        public string ProductCode { get; private set; }
        public string ShortName { get; private set; }
        public static List<GlobalSignCertType> AllTypes
        {
            get
            {
                return new List<GlobalSignCertType>
                {
                    new GlobalSignCertType { DisplayName = "ExtendedSSL SHA256", ProductCode = "PEV_SHA2", ShortName = "ExtendedSSL", HasWildCard = false, Type = GSCertificateType.EV },
                    new GlobalSignCertType{DisplayName="ExtendedSSL SHA1",ProductCode="PEV",ShortName="ExtendedSSL-Deprecated",HasWildCard=false,Type=GSCertificateType.EV},

                    new GlobalSignCertType { DisplayName = "OrganizationSSL SHA1", ProductCode = "PV", ShortName = "OrganizationSSL-Deprecated", HasWildCard = true, Type = GSCertificateType.OV},
                    new GlobalSignCertType { DisplayName = "OrganizationSSL SHA256", ProductCode = "PV_SHA2", ShortName = "OrganizationSSL", HasWildCard = true, Type = GSCertificateType.OV},

                    new GlobalSignCertType { DisplayName = "IntranetSSL SHA1", ProductCode = "PV_INTRA", ShortName = "IntranetSSL", HasWildCard = true, Type = GSCertificateType.Intranet },
                    new GlobalSignCertType { DisplayName = "IntranetSSL SHA2", ProductCode = "PV_INTRA_SHA2", ShortName = "IntranetSSL", HasWildCard = true, Type = GSCertificateType.Intranet },
                    new GlobalSignCertType { DisplayName = "IntranetSSL SHA256ECDSA", ProductCode = "PV_INTRA_ECCP256", ShortName = "IntranetSSL", HasWildCard = true, Type = GSCertificateType.Intranet },

                    new GlobalSignCertType { DisplayName = "CloudSSL SHA256", ProductCode = "PV_CLOUD", ShortName = "IntranetSSL", HasWildCard = true, Type = GSCertificateType.Cloud },
                    new GlobalSignCertType { DisplayName = "CloudSSL SHA256ECDSA", ProductCode = "PV_CLOUD_ECC2", ShortName = "IntranetSSL", HasWildCard = true, Type = GSCertificateType.Cloud },
                };
            }
        }

    }

    public enum GSCertificateType
    { 
        Intranet=1,
        OV=2,
        EV=3,
        Cloud=4
    }
}
