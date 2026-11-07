using DungeonOnline.Net;
using NUnit.Framework;

namespace DungeonOnline.Tests
{
    public class ServerListTests
    {
        private const int DefaultPort = 18889;

        [Test]
        public void TryParseAddress_HostOnly_UsesDefaultPort()
        {
            // Act
            bool ok = ServerList.TryParseAddress(" 192.168.1.5 ", DefaultPort, out string host, out int port);

            // Assert
            Assert.IsTrue(ok);
            Assert.AreEqual("192.168.1.5", host);
            Assert.AreEqual(DefaultPort, port);
        }

        [Test]
        public void TryParseAddress_HostAndPort_ReadsBoth()
        {
            // Act
            bool ok = ServerList.TryParseAddress("10.0.0.2:19000", DefaultPort, out string host, out int port);

            // Assert
            Assert.IsTrue(ok);
            Assert.AreEqual("10.0.0.2", host);
            Assert.AreEqual(19000, port);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(":18889")]
        [TestCase("host:abc")]
        [TestCase("host:70000")]
        public void TryParseAddress_Bad_ReturnsFalse(string text)
        {
            Assert.IsFalse(ServerList.TryParseAddress(text, DefaultPort, out _, out _));
        }
    }
}
