﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using GalaSoft.MvvmLight.Messaging;

namespace Prime.Core
{
    /// <summary>
    /// Singleton, one per application.
    /// </summary>
    public class ServerContext
    {
        public readonly IMessenger M;
        public readonly Users Users;
        public readonly PrimeServerConfig Config;
        public readonly DirectoryInfo ConfigDirectoryInfo;
        public readonly Platform PlatformCurrent;
        public readonly AssemblyCatalogue Assemblies;
        public readonly TypeCatalogue Types;
        public IReadOnlyList<IExtension> Extensions => _extensions;
        private readonly List<IExtension> _extensions = new List<IExtension>();

        public ServerContext() : this("[src]/instance/prime-server.config") { } //used for testing / debug

        public ServerContext(string configPath) : this(configPath, DefaultMessenger.I.DefaultServer) { }

        public ServerContext(string configPath, IMessenger m)
        {
            configPath = configPath.ResolveSpecial();

            PlatformCurrent = OsInformation.GetPlatform();

            _testing = this; //todo: hack for now.

            if (string.IsNullOrWhiteSpace(configPath))
                throw new ArgumentException($"\'{nameof(configPath)}\' cannot be empty.");

            Assemblies = new AssemblyCatalogue();
            Types = new TypeCatalogue(Assemblies);

            ConfigDirectoryInfo = new FileInfo(Path.GetFullPath(configPath)).Directory;
            Config = PrimeServerConfig.Get(Path.GetFullPath(configPath));
            M = m;
            Users = new Users(this);
            Public = new PublicContext(this);
        }

        public void AddInitialisedExtension(IExtension extension)
        {
            _extensions.RemoveAll(x => x.Id == extension.Id);
            _extensions.Add(extension);
        }

        private static ServerContext _testing = new ServerContext();
        public static ServerContext Testing => _testing ?? new ServerContext();

        public static PublicContext Public;

        private DirectoryInfo _appDataDirectoryInfo;
        public DirectoryInfo AppDataDirectoryInfo => _appDataDirectoryInfo ?? (_appDataDirectoryInfo = GetAppBase());

        private ILogger _logger;
        public ILogger L
        {
            get => _logger ?? (_logger = new MessengerLogger(M));
            set => _logger = value;
        }

        private ServerFileSystem _serverFileSystem;
        public ServerFileSystem FileSystem => _serverFileSystem ?? (_serverFileSystem = new ServerFileSystem(this));

        private DirectoryInfo GetAppBase()
        {
            if (!string.IsNullOrWhiteSpace(Config.BasePath))
                return new DirectoryInfo(Path.GetFullPath(Config.BasePath));

            if (Config.ConfigLoadedFrom != null)
                return Config.ConfigLoadedFrom.Directory;

            return new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        }
    }
}