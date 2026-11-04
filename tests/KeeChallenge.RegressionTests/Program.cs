using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using KeeChallenge;
using KeePass.Forms;
using KeePass.Plugins;
using KeePass.Util;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Serialization;
using Microsoft.CSharp;

internal static class Program
{
    private static readonly Assembly PluginAssembly = typeof(KeeChallengeExt).Assembly;
    private static int failures;

    [STAThread]
    private static int Main(string[] args)
    {
        foreach (DefaultTraceListener listener in Trace.Listeners.OfType<DefaultTraceListener>())
            listener.AssertUiEnabled = false;

        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: KeeChallenge.RegressionTests.exe <VERSION path>");
            return 1;
        }

        Console.WriteLine("Running .NET Framework regressions ({0}-bit).", IntPtr.Size * 8);
        Run("Plugin identity and fail-closed update URL", TestIdentity);
        Run("Strict secret parsing and sanitized validation messages", TestSecretParsing);
        Run("Sensitive buffer clearing", TestSensitiveData);
        Run("Diagnostics retain context and nested exceptions", TestDiagnostics);
        Run("Diagnostics tolerate failed trace and fallback sinks", TestDiagnosticFailures);
        Run("HMAC compatibility in both challenge modes", TestChallengeModes);
        Run("Decrypt success, wrong response, and corrupt ciphertext", TestDecryption);
        Run("Signed feed accepted; tampered and unsigned feeds rejected",
            () => TestUpdateFeed(Path.GetFullPath(args[0])));
        Run("Real KeePass loader accepts plugin and rejects renamed product", TestPluginLoader);
        Console.WriteLine("RESULT: {0} ({1} failed)", failures == 0 ? "PASS" : "FAIL", failures);
        return failures == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine("PASS: " + name);
        }
        catch (Exception ex)
        {
            ++failures;
            Console.Error.WriteLine("FAIL: {0}\n{1}", name, ex);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static MethodInfo Method(string type, string name, params Type[] parameters)
    {
        return PluginAssembly.GetType("KeeChallenge." + type, true).GetMethod(name,
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, parameters, null);
    }

    private static void TestIdentity()
    {
        Require(PluginAssembly.GetName().Name == "KeeChallenge", "Assembly name changed.");
        Require(PluginAssembly.GetCustomAttribute<AssemblyTitleAttribute>().Title == "KeeChallenge",
            "Assembly title must match the VERSION component.");
        Require(PluginAssembly.GetCustomAttribute<AssemblyProductAttribute>().Product == "KeePass Plugin",
            "KeePass silently filters plugins with a different AssemblyProduct.");
        Require(new KeeChallengeExt().UpdateUrl == string.Empty,
            "Updates must be disabled until signature verification is configured.");
        Require(!new KeeChallengeExt().Initialize(null), "Null host should be rejected.");
    }

    private static void TestSecretParsing()
    {
        MethodInfo parse = Method("SecretInputParser", "TryParseSecret",
            typeof(string), typeof(byte[]).MakeByRefType(), typeof(string).MakeByRefType());
        string valid = "00112233445566778899aAbBcCdDeEfF00112233";
        foreach (string input in new[] { null, "", "1234", valid + "00",
            "GG112233445566778899AABBCCDDEEFF00112233" })
        {
            object[] values = { input, null, null };
            Require(!(bool)parse.Invoke(null, values), "Malformed secret was accepted.");
            Require(values[1] == null, "Rejected input returned a secret.");
            string expected = input != null && input.Length == 40
                ? "Error: secret must contain only hexadecimal characters (0-9, A-F)."
                : "Error: secret must be exactly 20 bytes (40 hex characters).";
            Require((string)values[2] == expected, "Unexpected user-facing validation message.");
        }

        foreach (string input in new[] { valid, " \t" + valid.Insert(20, "\r\n ") + "\n" })
        {
            object[] values = { input, null, null };
            Require((bool)parse.Invoke(null, values), "Valid secret was rejected.");
            byte[] secret = (byte[])values[1];
            Require(secret.Length == 20 && BitConverter.ToString(secret).Replace("-", "") ==
                valid.ToUpperInvariant(), "Parsed secret differs from the input.");
            Require((string)values[2] == "", "Successful parsing returned an error.");
            Array.Clear(secret, 0, secret.Length);
        }
    }

    private static void TestSensitiveData()
    {
        byte[] first = { 1, 2, 3 };
        byte[] second = { 4, 5, 6 };
        Method("SensitiveData", "Clear", typeof(byte[][])).Invoke(null,
            new object[] { new byte[][] { first, null, new byte[0], second } });
        Require(first.All(b => b == 0) && second.All(b => b == 0), "Buffers were not cleared.");
        byte[] referenced = { 7, 8 };
        object[] values = { referenced };
        Method("SensitiveData", "Clear", typeof(byte[]).MakeByRefType()).Invoke(null, values);
        Require(values[0] == null && referenced.All(b => b == 0), "Reference was not cleared.");
    }

    private static void TestDiagnostics()
    {
        using (StringWriter output = new StringWriter())
        using (TextWriterTraceListener listener = new TextWriterTraceListener(output))
        {
            Trace.Listeners.Add(listener);
            try
            {
                Method("Diagnostics", "TraceException", typeof(string), typeof(Exception))
                    .Invoke(null, new object[] { "Regression diagnostic",
                        new InvalidOperationException("Outer diagnostic", new Exception("Inner diagnostic")) });
                listener.Flush();
                string text = output.ToString();
                Require(text.Contains("[KeeChallenge]") && text.Contains("Regression diagnostic") &&
                    text.Contains("Outer diagnostic") && text.Contains("Inner diagnostic"),
                    "Trace output lost context or nested exception details.");
            }

            finally
            {
                Trace.Listeners.Remove(listener);
            }
        }
    }

    private sealed class FailingTraceListener : TraceListener
    {
        public override void Write(string message) { throw new IOException("Test trace sink failure"); }
        public override void WriteLine(string message) { throw new IOException("Test trace sink failure"); }
    }

    private sealed class FailingTextWriter : TextWriter
    {
        public override Encoding Encoding { get { return Encoding.UTF8; } }
        public override void WriteLine(string value) { throw new IOException("Test fallback sink failure"); }
    }

    private static void TestDiagnosticFailures()
    {
        Type diagnostics = PluginAssembly.GetType("KeeChallenge.Diagnostics", true);
        PropertyInfo count = diagnostics.GetProperty("FailureCount", BindingFlags.Static | BindingFlags.NonPublic);
        int before = (int)count.GetValue(null);
        TextWriter original = Console.Error;
        using (FailingTraceListener listener = new FailingTraceListener())
        using (StringWriter fallback = new StringWriter())
        {
            Trace.Listeners.Add(listener);
            try
            {
                Console.SetError(fallback);
                Method("Diagnostics", "TraceException", typeof(string), typeof(Exception))
                    .Invoke(null, new object[] { "Failure test", new Exception("Test exception") });
                Require(fallback.ToString().Contains("Diagnostic output failed"),
                    "Trace failure was not reported through the fallback.");
                using (FailingTextWriter failedFallback = new FailingTextWriter())
                {
                    Console.SetError(failedFallback);
                    Method("Diagnostics", "TraceException", typeof(string), typeof(Exception))
                        .Invoke(null, new object[] { "Failure test", new Exception("Test exception") });
                }
                Require((int)count.GetValue(null) == before + 3,
                    "Diagnostic sink failures were not accounted for.");
            }
            finally
            {
                Console.SetError(original);
                Trace.Listeners.Remove(listener);
            }
        }
    }

    private static void TestChallengeModes()
    {
        byte[] secret = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
        foreach (bool lt64 in new[] { false, true })
        {
            KeeChallengeProv provider = new KeeChallengeProv { LT64 = lt64 };
            byte[] challenge = provider.GenerateChallenge();
            byte[] snapshot = (byte[])challenge.Clone();
            Require(challenge.Length == 64, "Stored challenges must remain 64 bytes.");
            using (HMACSHA1 hmac = new HMACSHA1(secret))
            {
                byte[] expected = hmac.ComputeHash(lt64 ? challenge.Take(63).ToArray() : challenge);
                Require(provider.GenerateResponse(challenge, secret).SequenceEqual(expected),
                    "Challenge response compatibility changed.");
            }
            Require(challenge.SequenceEqual(snapshot), "Caller-owned challenge was modified.");
            Require(secret.SequenceEqual(Enumerable.Range(0, 20).Select(i => (byte)i)),
                "Caller-owned secret was modified.");
        }
    }

    private static void TestDecryption()
    {
        byte[] secret = Enumerable.Range(1, 20).Select(i => (byte)i).ToArray();
        byte[] response = Enumerable.Range(21, 20).Select(i => (byte)i).ToArray();
        byte[] verification;
        byte[] encrypted;
        byte[] iv;
        using (SHA256 sha = SHA256.Create())
        using (Aes aes = Aes.Create())
        {
            aes.Key = sha.ComputeHash(response);
            aes.GenerateIV();
            iv = aes.IV;
            verification = sha.ComputeHash(secret);
            using (ICryptoTransform encryptor = aes.CreateEncryptor())
                encrypted = encryptor.TransformFinalBlock(secret, 0, secret.Length);
        }

        MethodInfo decrypt = Method("KeeChallengeProv", "DecryptSecret",
            typeof(byte[]), typeof(byte[]), typeof(byte[]), typeof(byte[]),
            typeof(byte[]).MakeByRefType());
        object[] valid = { encrypted, response, iv, verification, null };
        Require((bool)decrypt.Invoke(null, valid) && ((byte[])valid[4]).SequenceEqual(secret),
            "Correct response did not recover the secret.");

        foreach (object[] invalid in new[]
        {
            new object[] { encrypted, new byte[20], iv, verification, null },
            new object[] { new byte[17], response, iv, verification, null },
            new object[] { encrypted, response, iv, new byte[32], null }
        })
        {
            Require(!(bool)decrypt.Invoke(null, invalid), "Invalid response or payload was accepted.");
            Require(((byte[])invalid[4]).All(b => b == 0), "Failed decrypt left a plaintext secret.");
        }
    }

    private static void TestUpdateFeed(string versionPath)
    {
        object[] configure = { null };
        Require((bool)Method("UpdateFeedSecurity", "TryConfigure",
            typeof(string).MakeByRefType()).Invoke(null, configure), "Signing setup failed: " + configure[0]);

        Type security = PluginAssembly.GetType("KeeChallenge.UpdateFeedSecurity", true);
        string url = (string)security.GetField("UpdateFeedUrl",
            BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        MethodInfo load = typeof(UpdateCheckEx).GetMethod("LoadInfoFilePriv",
            BindingFlags.Static | BindingFlags.NonPublic);
        string feed = File.ReadAllText(versionPath);
        string expectedVersion = PluginAssembly.GetName().Version.ToString(3);
        Require(feed.Contains("KeeChallenge:" + expectedVersion), "VERSION and assembly are out of sync.");
        object accepted = load.Invoke(null, new object[] { Encoding.UTF8.GetBytes(feed),
            IOConnectionInfo.FromPath(url) });
        Require(accepted != null && ((System.Collections.ICollection)accepted).Count == 1,
            "KeePass rejected the signed VERSION feed.");

        foreach (string invalid in new[]
        {
            feed.Replace("KeeChallenge:", "KeeChallengeX:"),
            ":\r\nKeeChallenge:" + expectedVersion + "\r\n:\r\n",
            ":AAAA\r\nKeeChallenge:" + expectedVersion + "\r\n:\r\n"
        })
        {
            Require(load.Invoke(null, new object[] { Encoding.UTF8.GetBytes(invalid),
                IOConnectionInfo.FromPath(url) }) == null, "KeePass accepted an unauthentic feed.");
        }
    }

    private static void TestPluginLoader()
    {
        string fixtureDirectory = Path.Combine(Path.GetTempPath(), "KeeChallenge-loader-" + Guid.NewGuid());
        Directory.CreateDirectory(fixtureDirectory);
        try
        {
            using (CSharpCodeProvider compiler = new CSharpCodeProvider())
            {
                CompilerParameters options = new CompilerParameters
                {
                    OutputAssembly = Path.Combine(fixtureDirectory, "RenamedPlugin.dll")
                };
                options.ReferencedAssemblies.Add(typeof(Plugin).Assembly.Location);
                CompilerResults result = compiler.CompileAssemblyFromSource(options,
                    "[assembly: System.Reflection.AssemblyProduct(\"KeeChallenge KeePass Plugin\")]" +
                    "namespace RenamedPlugin { public sealed class RenamedPluginExt : KeePass.Plugins.Plugin {" +
                    "public override bool Initialize(KeePass.Plugins.IPluginHost host) { return true; } } }");
                Require(!result.Errors.HasErrors, "Could not compile renamed-product regression fixture.");
            }

            using (MainForm form = new MainForm())
            {
                Assembly keepass = typeof(Plugin).Assembly;
                Type hostType = keepass.GetType("KeePass.Plugins.DefaultPluginHost", true);
                IPluginHost host = (IPluginHost)Activator.CreateInstance(hostType);
                hostType.GetMethod("Initialize").Invoke(host,
                    new object[] { form, new CommandLineArgs(new string[0]), CipherPool.GlobalPool });
                Type managerType = keepass.GetType("KeePass.Plugins.PluginManager", true);
                object manager = Activator.CreateInstance(managerType);
                managerType.GetMethod("Initialize").Invoke(manager, new object[] { host });
                MethodInfo load = managerType.GetMethod("LoadAllPlugins",
                    new[] { typeof(string), typeof(SearchOption), typeof(string[]) });
                System.Collections.IEnumerable plugins = (System.Collections.IEnumerable)manager;
                try
                {
                    load.Invoke(manager, new object[] { fixtureDirectory, SearchOption.TopDirectoryOnly, null });
                    Require(!plugins.Cast<object>().Any(), "KeePass unexpectedly accepted the renamed product.");
                    load.Invoke(manager, new object[] { Path.GetDirectoryName(PluginAssembly.Location),
                        SearchOption.TopDirectoryOnly, null });
                    Require(plugins.Cast<object>().Count() == 1, "KeePass did not load exactly one plugin.");
                    Require(host.KeyProviderPool.Get(KeeChallengeProv.m_name) != null,
                        "Plugin loaded but did not register the key provider.");
                    Require(form.ToolsMenu.DropDownItems.Cast<ToolStripItem>()
                        .Any(item => item.Text == "KeeChallenge Settings"), "Settings menu was not registered.");
                    object info = plugins.Cast<object>().Single();
                    Plugin instance = (Plugin)info.GetType().GetProperty("Interface").GetValue(info);
                    Require(instance.UpdateUrl ==
                        "https://raw.githubusercontent.com/tdhayer/KeeChallenge-Modern/master/VERSION",
                        "Signed update feed was not enabled after initialization.");
                }
                finally
                {
                    managerType.GetMethod("UnloadAllPlugins").Invoke(manager, null);
                }
                Require(host.KeyProviderPool.Get(KeeChallengeProv.m_name) == null,
                    "Plugin termination left the key provider registered.");
                Require(!form.ToolsMenu.DropDownItems.Cast<ToolStripItem>()
                    .Any(item => item.Text == "KeeChallenge Settings"), "Termination left the settings menu.");
            }
        }
        finally
        {
            foreach (string file in Directory.GetFiles(fixtureDirectory))
                File.Delete(file);
            Directory.Delete(fixtureDirectory);
        }
    }
}
