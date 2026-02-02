using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc.UnitTest
{
    public class CertificateContext
    {
        public CertificateContext()
        {
            if (!File.Exists("server.pem") || !File.Exists("client.pem"))
            {
                using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384))
                {
                    var request = new CertificateRequest(new X500DistinguishedName($"CN=localhost"), ecdsa, HashAlgorithmName.SHA256);
                    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
                    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
                    request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
                    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false)); // OID for Server Authentication
                    var sanBuilder = new SubjectAlternativeNameBuilder();
                    sanBuilder.AddDnsName("localhost");
                    request.CertificateExtensions.Add(sanBuilder.Build());
                    ServerCertificate = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(20));
                    File.WriteAllText("server.pem", ServerCertificate.ExportCertificatePem() + "\n" + ecdsa.ExportPkcs8PrivateKeyPem());
                }
                using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384))
                {
                    var request = new CertificateRequest(new X500DistinguishedName($"CN=1"), ecdsa, HashAlgorithmName.SHA256);
                    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
                    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
                    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2") }, false)); // OID for Client Authentication                    
                    request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
                    var clientIdBytes = BitConverter.GetBytes(1);
                    var clientIdExtension = new X509Extension("2.23.42.2.5", clientIdBytes, false);
                    request.CertificateExtensions.Add(clientIdExtension);
                    var serialNumber = new byte[16];
                    using (var rng = RandomNumberGenerator.Create())
                        rng.GetBytes(serialNumber);
                    ClientCertificate = request.Create(ServerCertificate, DateTimeOffset.Now, ServerCertificate.NotAfter.AddDays(-1), serialNumber);
                    File.WriteAllText("client.pem", ClientCertificate.ExportCertificatePem() + "\n" + ecdsa.ExportPkcs8PrivateKeyPem());
                }
            }
            else
            {
                ServerCertificate = X509Certificate2.CreateFromPemFile("server.pem");
                ClientCertificate = X509Certificate2.CreateFromPemFile("client.pem");
            }
            ServerCertificate = X509CertificateLoader.LoadPkcs12(ServerCertificate.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
            ClientCertificate = X509CertificateLoader.LoadPkcs12(ClientCertificate.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
        }

        public X509Certificate2 ServerCertificate { get; }

        public X509Certificate2 ClientCertificate { get; }
    }
}
