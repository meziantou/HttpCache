using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Fiddler;
using Meziantou.Framework;
using Microsoft.AspNetCore.WebUtilities;

namespace HttpCache
{
    internal static class CacheProxy
    {
        private static readonly object FromCache = new object();

        private static Proxy _proxyEndpoint;
        private static IReadOnlyList<Filter> _filters = new List<Filter>();

        private static readonly Guid _providerGuid = new Guid("1b415595-30f8-4a70-94b2-2a0aaeeb7064");
        private static readonly EventProvider _provider = new EventProvider(_providerGuid);

        private static int _currentRequests = 0;

        public static void SetConfiguration(string configuration)
        {
            var filters = new List<Filter>();
            using (var sr = new StringReader(configuration))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                        continue;

                    filters.Add(new Filter(line));
                }
            }

            _filters = filters;
            _provider.WriteMessageEvent($"Configuration changed: ({filters.Count} filters){Environment.NewLine}{configuration}");
        }

        public static void Start()
        {
            _provider.WriteMessageEvent("Starting proxy...");
            FiddlerApplication.BeforeRequest += OnBeforeRequest;
            FiddlerApplication.AfterSessionComplete += OnAfterSessionComplete;

            var startupSettings = new FiddlerCoreStartupSettingsBuilder()
              .ListenOnPort(8877)
              .RegisterAsSystemProxy()
              .DecryptSSL()
              .ChainToUpstreamGateway()
              .MonitorAllConnections()
              //.CaptureLocalhostTraffic()
              .OptimizeThreadPool()
              .Build();

            FiddlerApplication.Startup(startupSettings);
            _proxyEndpoint = FiddlerApplication.CreateProxyEndpoint(48721, true, "localhost");

            _provider.WriteMessageEvent("Proxy started");
        }

        public static void Stop()
        {
            _provider.WriteMessageEvent("Stopping proxy...");
            if (_proxyEndpoint != null)
            {
                _proxyEndpoint.Detach();
                _proxyEndpoint.Dispose();
            }

            FiddlerApplication.BeforeRequest -= OnBeforeRequest;
            FiddlerApplication.AfterSessionComplete -= OnAfterSessionComplete;
            if (FiddlerApplication.IsStarted())
            {
                FiddlerApplication.Shutdown();
            }

            _provider.WriteMessageEvent("Proxy stopped");
        }

        public static bool IsStarted() => FiddlerApplication.IsStarted();

        private static void OnBeforeRequest(Session session)
        {
            Interlocked.Increment(ref _currentRequests);

            if (!ShouldProcess(session))
                return;

            var file = GetCacheFile(session);
            if (File.Exists(file))
            {
                _provider.WriteMessageEvent($"Using cache: {session.RequestMethod} {session.fullUrl}");

                session.utilCreateResponseAndBypassServer();
                session.LoadResponseFromFile(file);
                session.Tag = FromCache;
            }
        }

        private static void OnAfterSessionComplete(Session session)
        {
            Interlocked.Decrement(ref _currentRequests);

            if (session.Tag == FromCache)
                return;

            if (!ShouldProcess(session))
                return;

            _provider.WriteMessageEvent($"Adding to cache: {session.RequestMethod} {session.fullUrl}");

            var cacheFile = GetCacheFile(session);
            IOUtilities.PathCreateDirectory(cacheFile);
            try
            {
                session.SaveResponse(cacheFile, bHeadersOnly: false);
            }
            catch (Exception ex)
            {
                _provider.WriteMessageEvent($"Error while adding to cache {session.RequestMethod} {session.fullUrl}: {ex}");
            }
        }

        private static bool ShouldProcess(Session session)
        {
            foreach (var filter in _filters)
            {
                if (filter.Match(session))
                    return true;
            }

            return false;
        }

        private static string GetCacheFile(Session session)
        {
            // Filter values
            var url = GetCacheUrl(session.fullUrl, new[] { "private_token" });

            // Be careful of case sensitivity
            var fileName = IOUtilities.ToValidFileName(url);
            if (fileName.Length > 50)
            {
                fileName = fileName.Substring(0, 50);
            }

            return Path.Combine(Path.GetTempPath(), "HttpCache", "Cache", fileName + '_' + GetHash(url) + ".cache");
        }

        public static void ClearCache()
        {
            var path = Path.Combine(Path.GetTempPath(), "HttpCache", "Cache");
            if (!Directory.Exists(path))
                return;

            foreach (var file in Directory.GetFiles(path))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    _provider.WriteMessageEvent($"Error while deleting file '{file}': {ex}");
                }
            }
        }

        private static string GetHash(string url)
        {
            using (var hash = SHA1.Create())
            {
                return hash.ComputeHash(Encoding.UTF8.GetBytes(url)).ToHexa(HexaOptions.LowerCase);
            }
        }

        private static string GetCacheUrl(string url, string[] paramsToRemove)
        {
            if (paramsToRemove == null || !paramsToRemove.Any())
                return url;

            var indexOfHash = url.IndexOf('#');
            if (indexOfHash > 0)
            {
                url = url.Substring(0, indexOfHash);
            }

            var indexOfQueryString = url.IndexOf('?');
            if (indexOfQueryString < 0)
                return url;

            var querystring = url.Substring(indexOfQueryString);
            var query = QueryHelpers.ParseQuery(querystring);

            var result = url.Substring(0, indexOfQueryString);
            foreach (var kvp in query)
            {
                if (!paramsToRemove.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    foreach (var value in kvp.Value)
                    {
                        result = QueryHelpers.AddQueryString(result, kvp.Key, value);
                    }
                }
            }

            return result;
        }
    }
}
