using System.Collections.Generic;
using NAS.Core.Networking;

namespace NAS.Core
{
    /// <summary>Turns the server's answer into a feature set. Lives beside DealershipFeatureSet (NAS.Core), not with the DTO, because the networking assembly cannot see NAS.Core.</summary>
    public static class DealershipFeaturesReader
    {
        /// <summary>The server's answer as a feature set. A missing or empty answer is everything on (see <see cref="DealershipFeatureSet"/>).</summary>
        public static DealershipFeatureSet ToFeatureSet(DealershipFeaturesResponse response)
        {
            if (response?.features == null) return DealershipFeatureSet.AllOn;
            var states = new List<KeyValuePair<string, bool>>();
            foreach (var feature in response.features)
                if (feature != null) states.Add(new KeyValuePair<string, bool>(feature.key, feature.enabled));
            return DealershipFeatureSet.From(states);
        }
    }
}
