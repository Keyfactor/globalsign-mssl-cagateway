using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GlobalSignCAProxyTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            string dt = $"{DateTime.Now:yyyy-MM-dd'T'HH:mm:ss'.000Z'}";
            string dt1 = $"{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffZ}";
            var cnArray = "sub1.sub2.sub-3.host.domain.com".Split('.');
            var domainValue = $"{cnArray[cnArray.Length - 2]}.{cnArray[cnArray.Length - 1]}";
        }
    }
}
