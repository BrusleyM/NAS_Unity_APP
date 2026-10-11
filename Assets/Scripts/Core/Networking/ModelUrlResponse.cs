using System;

namespace NAS.Core.Networking
{
    // GET api/customer/vehicles/{id}/model-url: a pre-signed, short-lived download link for the vehicle's 3D model.
    [Serializable]
    public sealed class ModelUrlResponse
    {
        public string url;
        public int expiresInSeconds;
    }
}
