using System;

namespace NAS.Core.Networking
{
    // Mirrors CustomerDealershipDto/CustomerDealershipListResponseDto from
    // GET /api/customer/dealerships - just enough to recognise a dealership
    // (no staff/lead counts). Null strings are fine, JsonUtility leaves them null.
    [Serializable]
    public sealed class CustomerDealershipDto
    {
        public int id;
        public string name;
        public string address;
        public string phone;
        public string brandingLogoUrl;
        public string primaryColor;
    }

    [Serializable]
    public sealed class CustomerDealershipListResponse
    {
        public CustomerDealershipDto[] dealerships;
    }
}
