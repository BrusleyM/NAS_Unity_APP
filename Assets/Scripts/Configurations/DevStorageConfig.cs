using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using NAS.Core.Interfaces;
using UnityEngine;

namespace NAS.Configuration
{
    [CreateAssetMenu(fileName = "DevStorageConfig", menuName = "NAS/Dev Storage Config")]
    public class DevStorageConfig : ScriptableObject, IStorageConfig
    {
        [SerializeField] private string _devAccessKey;
        [SerializeField] private string _devSecretKey;
        [SerializeField] private string _devServiceUrl = "https://fly.storage.tigris.dev";
        [SerializeField] private string _devBucketName;

        // False for an environment whose key has not been put in yet; the app then logs an error and downloads nothing
        // (it falls back to the placeholder car) instead of crashing on an empty key.
        public bool HasCredentials => !string.IsNullOrWhiteSpace(_devAccessKey) && !string.IsNullOrWhiteSpace(_devSecretKey);

        public string ServiceUrl => _devServiceUrl;
        public string BucketName => _devBucketName;

        public IAmazonS3 CreateS3Client()
        {
            var credentials = new BasicAWSCredentials(_devAccessKey, _devSecretKey);
            var config = new AmazonS3Config
            {
                ServiceURL = ServiceUrl,
                UseHttp = false,
                ForcePathStyle = true
            };
            return new AmazonS3Client(credentials, config);
        }
    }
}