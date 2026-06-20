// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
// Checks whether the local Intel SDM PDF used for extraction is the latest revision.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AsmAnnotate
{
    /// <summary>
    /// Where the Intel documentation comes from, and how to tell whether the
    /// local copy used for extraction is still the latest one Intel publishes.
    ///
    /// SOURCE OF TRUTH
    /// ===============
    /// The Intel® 64 and IA-32 Architectures Software Developer's Manual
    /// (combined volumes 1, 2A-2D, 3A-3D, 4) has Intel document/order number
    /// <b>325462</b>. Intel hosts a permanent "latest" redirect that always
    /// resolves to the newest revision:
    ///
    ///     https://cdrdv2.intel.com/v1/dl/getContent/671200
    ///         -> https://cdrdv2-public.intel.com/&lt;contentId&gt;/325462-&lt;rev&gt;-sdm-vol-1-2abcd-3abcd-4[-vN].pdf
    ///
    /// The numeric &lt;contentId&gt; prefix changes with every release, but the
    /// revision number is embedded in the filename as "325462-&lt;rev&gt;-sdm...".
    /// That revision is the value we compare against the local file.
    ///
    /// Human landing page (lists the current revision of every volume):
    ///     https://www.intel.com/content/www/us/en/developer/articles/technical/intel-sdm.html
    /// </summary>
    public static partial class IntelDocChecker
    {
        /// <summary>
        /// Intel's permanent redirect to the latest combined-volumes SDM (document 325462).
        /// Following this URL yields a filename that encodes the current revision number.
        /// </summary>
        public const string LatestCombinedVolumesRedirectUrl = "https://cdrdv2.intel.com/v1/dl/getContent/671200";

        /// <summary>
        /// Matches the revision number in an Intel SDM combined-volumes filename,
        /// e.g. "325462-091-sdm-vol-1-2abcd-3abcd-4-v2.pdf" -> 091.
        /// </summary>
        [GeneratedRegex(@"325462-(?<rev>\d+)-sdm", RegexOptions.IgnoreCase, 2000)]
        private static partial Regex RevisionRegex();

        /// <summary>
        /// Result of a freshness check.
        /// </summary>
        public sealed class CheckResult
        {
            /// <summary>Revision parsed from the local PDF, or null if none found.</summary>
            public int? LocalRevision { get; init; }

            /// <summary>Path of the local PDF that was inspected, or null if none found.</summary>
            public string? LocalPath { get; init; }

            /// <summary>Latest revision Intel currently publishes, or null if the check could not reach Intel.</summary>
            public int? LatestRevision { get; init; }

            /// <summary>Filename Intel's redirect resolved to (e.g. "325462-091-sdm-vol-1-2abcd-3abcd-4-v2.pdf").</summary>
            public string? LatestFileName { get; init; }

            /// <summary>Direct download URL for the latest revision, when the online check succeeded.</summary>
            public string? LatestDownloadUrl { get; init; }

            /// <summary>Populated when the online check failed (offline, blocked, etc.).</summary>
            public string? Error { get; init; }

            /// <summary>True only when both revisions are known and the local one is current.</summary>
            public bool IsUpToDate => LocalRevision.HasValue && LatestRevision.HasValue && LocalRevision.Value >= LatestRevision.Value;
        }

        /// <summary>
        /// Parses the revision number out of an Intel SDM combined-volumes filename.
        /// Returns null when the name does not match the expected pattern.
        /// </summary>
        public static int? ParseRevision(string fileNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(fileNameOrPath)) return null;
            Match m = RevisionRegex().Match(fileNameOrPath);
            if (m.Success && int.TryParse(m.Groups["rev"].Value, out int rev))
                return rev;
            return null;
        }

        /// <summary>
        /// Finds the local combined-volumes SDM PDF (325462-*.pdf) in <paramref name="dataDir"/>,
        /// preferring the highest revision when several are present.
        /// </summary>
        public static (string? Path, int? Revision) FindLocalPdf(string dataDir)
        {
            if (!Directory.Exists(dataDir))
                return (null, null);

            var best = Directory
                .EnumerateFiles(dataDir, "325462-*.pdf")
                .Select(p => (Path: p, Rev: ParseRevision(System.IO.Path.GetFileName(p))))
                .Where(t => t.Rev.HasValue)
                .OrderByDescending(t => t.Rev!.Value)
                .FirstOrDefault();

            return (best.Path, best.Rev);
        }

        /// <summary>
        /// Asks Intel (via the permanent redirect) which revision is current, without
        /// downloading the whole PDF. Returns the revision, resolved filename, and the
        /// final download URL. Throws nothing fatal: callers should prefer
        /// <see cref="CheckAsync"/>, which wraps failures into a <see cref="CheckResult"/>.
        /// </summary>
        public static async Task<(int Revision, string FileName, string Url)> GetLatestRevisionAsync(CancellationToken ct = default)
        {
            // Follow redirects so RequestMessage.RequestUri ends up at the public CDN URL.
            using var handler = new HttpClientHandler { AllowAutoRedirect = true };
            using var http = new HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(30);
            // Some Intel edge nodes 403 a bare/unknown agent; mirror a normal browser.
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            // HEAD is enough: we only need the resolved filename, not the 26 MB body.
            using var request = new HttpRequestMessage(HttpMethod.Head, LatestCombinedVolumesRedirectUrl);
            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // Prefer the Content-Disposition filename; fall back to the final redirected URL.
            string? fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                               ?? response.Content.Headers.ContentDisposition?.FileName;
            string finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? LatestCombinedVolumesRedirectUrl;
            if (string.IsNullOrEmpty(fileName))
                fileName = System.IO.Path.GetFileName(response.RequestMessage?.RequestUri?.AbsolutePath ?? "");

            fileName = fileName?.Trim('"');
            int? rev = ParseRevision(fileName ?? finalUrl);
            if (!rev.HasValue)
                throw new InvalidOperationException($"Could not parse revision from Intel response (file='{fileName}', url='{finalUrl}').");

            return (rev.Value, fileName ?? $"325462-{rev.Value:000}-sdm-vol-1-2abcd-3abcd-4.pdf", finalUrl);
        }

        /// <summary>
        /// Compares the local SDM PDF in <paramref name="dataDir"/> against the revision Intel
        /// currently publishes. Never throws for network problems — failures are reported via
        /// <see cref="CheckResult.Error"/> so the extraction pipeline can decide whether to continue.
        /// </summary>
        public static async Task<CheckResult> CheckAsync(string dataDir, CancellationToken ct = default)
        {
            (string? localPath, int? localRev) = FindLocalPdf(dataDir);

            try
            {
                (int latestRev, string latestFile, string url) = await GetLatestRevisionAsync(ct).ConfigureAwait(false);
                return new CheckResult
                {
                    LocalRevision = localRev,
                    LocalPath = localPath,
                    LatestRevision = latestRev,
                    LatestFileName = latestFile,
                    LatestDownloadUrl = url,
                };
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                return new CheckResult
                {
                    LocalRevision = localRev,
                    LocalPath = localPath,
                    Error = ex.Message,
                };
            }
        }

        /// <summary>
        /// Runs <see cref="CheckAsync"/> and prints a human-readable verdict to the console.
        /// Returns true when the local PDF is confirmed current (callers may gate extraction on this).
        /// </summary>
        public static async Task<bool> CheckAndReportAsync(string dataDir, CancellationToken ct = default)
        {
            Console.WriteLine("Checking Intel SDM revision (document 325462, combined volumes)...");
            Console.WriteLine($"  Source: {LatestCombinedVolumesRedirectUrl}");

            CheckResult result = await CheckAsync(dataDir, ct).ConfigureAwait(false);

            Console.WriteLine(result.LocalRevision is int lr
                ? $"  Local : revision {lr:000}  ({result.LocalPath})"
                : $"  Local : no 325462-*.pdf found in {dataDir}");

            if (result.Error != null)
            {
                Console.WriteLine($"  Latest: UNKNOWN — could not reach Intel ({result.Error})");
                Console.WriteLine("  Verdict: ⚠️  Unable to verify; check manually at");
                Console.WriteLine("           https://www.intel.com/content/www/us/en/developer/articles/technical/intel-sdm.html");
                return false;
            }

            Console.WriteLine($"  Latest: revision {result.LatestRevision:000}  ({result.LatestFileName})");

            if (result.IsUpToDate)
            {
                Console.WriteLine("  Verdict: ✅ Up to date.");
                return true;
            }

            if (result.LocalRevision is null)
                Console.WriteLine("  Verdict: ❌ No local PDF. Download the latest:");
            else
                Console.WriteLine($"  Verdict: ❌ Outdated (local {result.LocalRevision:000} < latest {result.LatestRevision:000}). Download the latest:");

            Console.WriteLine($"           {result.LatestDownloadUrl}");
            Console.WriteLine($"           (save into {dataDir})");
            return false;
        }
    }
}
