using UnityEngine;

namespace NAS.Core.Networking
{
    [CreateAssetMenu(fileName = "ApiSettings", menuName = "NAS/Networking/API Settings")]
    public sealed class ApiSettings : ScriptableObject
    {
        [SerializeField] private string _baseUrl = "http://localhost:5080";
        [SerializeField, Min(1)] private int _timeoutSeconds = 15;

        public string BaseUrl => _baseUrl.TrimEnd('/');
        public int TimeoutSeconds => _timeoutSeconds;
    }
}
