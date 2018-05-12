﻿using System.Collections.Generic;
using Prime.Core.Encryption;
using Prime.Core;
using Prime.Extensions;

namespace Prime.ConsoleApp.Tests.Frank
{
    public class Frank
    {
        public static void Go(ServerContext s, ClientContext c)
        {
            //new ReflectionTests(s).Go();

            //new IpfsMessageTest(s).Go();

            new SocketServerTestClientServer(s, c).Go();

            //AuthManagerTest.EcdsaKeySign(s);

            //new ExtensionLoader().Compose();

            //PackageTests.PackageCoordinator(s);

            //PackageTests.PackageCatalogue(s);
        }
    }
}
