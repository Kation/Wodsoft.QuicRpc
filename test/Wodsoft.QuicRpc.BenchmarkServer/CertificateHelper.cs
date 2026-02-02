using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc.BenchmarkServer
{
    internal class CertificateHelper
    {
        static CertificateHelper()
        {
            if (!File.Exists("server.pem"))
            {
                using (var rsa = RSA.Create())
                {
                    var request = new CertificateRequest(new X500DistinguishedName($"CN=localhost"), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
                    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
                    request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
                    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false)); // OID for Server Authentication
                    var sanBuilder = new SubjectAlternativeNameBuilder();
                    sanBuilder.AddDnsName("localhost");
                    request.CertificateExtensions.Add(sanBuilder.Build());
                    ServerCertificate = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(20));
                    File.WriteAllText("server.pem", ServerCertificate.ExportCertificatePem() + "\n" + rsa.ExportPkcs8PrivateKeyPem());
                }
            }
            else
            {
                ServerCertificate = X509Certificate2.CreateFromPemFile("server.pem");
            }
            ServerCertificate = X509CertificateLoader.LoadPkcs12(ServerCertificate.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
        }

        public static X509Certificate2 ServerCertificate { get; }
    }
}
