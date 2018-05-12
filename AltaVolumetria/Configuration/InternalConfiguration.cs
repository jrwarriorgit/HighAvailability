using Microsoft.Azure;
using Microsoft.Azure.KeyVault;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Configuration
{
    public class InternalConfiguration
    {
        public static string ApplicationId = "2a19cb74-291c-484f-bb77-c12d44ed5843";// CloudConfigurationManager.GetSetting("ApplicationId");
        public static string ApplicationKey = "7d694ec8-7177-4176-800a-921a81bec6d8";// CloudConfigurationManager.GetSetting("ApplicationKey");
        public static string KeyVaultAddress = "https://dmkvSecretsdemoraul.vault.azure.net/";//CloudConfigurationManager.GetSetting("KeyVaultAddress");


        private static string[] _storages=null;
        private static Dictionary<string, string> _secrets = new Dictionary<string, string>();
        private static Dictionary<string, bool> _banderas = new Dictionary<string, bool>();

        public static async Task<string> GetAccessToken(string authority, string resource, string clientId, string clientSecret)
        {
            var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
            var clientCredentials = new ClientCredential(clientId, clientSecret);
            var result = await context.AcquireTokenAsync(resource, clientCredentials).ConfigureAwait(false);

            return result.AccessToken;
        }

        public static string QueueConnectionString { get { return GetSecret("QueueConnectionString"); } }
        public static string RedisConnectionString { get { return GetSecret("RedisConnectionString"); } }
        public static bool EnableRedisCache{ get { return GetBandera("EnableRedis","false"); } }
        public static bool EnableInLineXML { get { return GetBandera("EnableInLineXML","false"); } }
        public static bool EnableSqlBulkInsert { get { return GetBandera("EnableSqlBulkInsert","true"); } }
        public static string[] Storages { get { return GetConfigurationArray(_storages, "Storage"); } }
        public static string Name { get { return GetSecret("Name"); } }
        public static string Kid { get { return GetSecret("Kid"); } }
        public static int NumberOfKeyVaults { get { return Convert.ToInt32( GetSecret("NumberOfKeyVaults","1")); } }
        public static string KeyVersion { get { return Kid.Replace($"https://dmkeypac{Name}01.vault.azure.net/keys/SignKey/",""); } }

        public static string CosmosDatabase { get { return "Facturas"; } }
        public static string CosmosCollection { get { return "Cfdi"; } }
        public static string CosmosEndpoint { get { return "https://dmcosmosdb.documents.azure.com:443/"; } }
        public static string CosmosAuthKey { get { return "A3s00h0Q5hODOYc0CJ4JHbe7BD2za3lGlZTpQJcSqHndgukoJxGj6k6BU37gp0BN3ArGK8GnGizArFt2CHvFXw=="; } }


        private static string GetSecret( string secretName, string defaultValue="")
        {
            if(!_secrets.ContainsKey(secretName) || refreshNeeded)
                _secrets[secretName] = AppSettings(secretName, defaultValue);

            return _secrets[secretName];
        }
        private static bool GetBandera( string secretName, string defaultValue="false")
        {
            if (!_banderas.ContainsKey(secretName) || refreshNeeded )
                _banderas[secretName] = AppSettings(secretName,defaultValue).ToLower()=="true";

            return _banderas[secretName];
        }

        private static string[] GetConfigurationArray(string[] localVariable, string secretName)
        {
            if (localVariable == null || refreshNeeded)
            {
                var count = 1;
                var response = new List<string>();
                string secret = null;
                do
                {
                    try
                    {
                        secret = AppSettings($"{secretName}{count:D3}");
                        response.Add(secret);
                        count++;
                    }
                    catch
                    {
                        secret = null;
                    }
                } while (!string.IsNullOrEmpty(secret));
                localVariable= response.ToArray();
            }
            return localVariable;
        }


        public static string SqlConnectionString { get { return AppSettings("SqlConnectionString"); } }

        //TODO: Logic about refresh
        public static bool refreshNeeded { get { return false; } }

        static KeyVaultClient keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(
                  (authority, resource, scope) => GetAccessToken(authority, resource, ApplicationId, ApplicationKey)),
                  new HttpClient()); 
        public static string AppSettings(string secretName, string defaultValue="")
        {
            Console.WriteLine($"Secret->{secretName}");
            string returnValue;
            try
            {
                returnValue = keyVaultClient.GetSecretAsync(KeyVaultAddress, secretName).Result.Value;
            }
            catch
            {
                return defaultValue;
            }
            return returnValue;
        }
    }

}
