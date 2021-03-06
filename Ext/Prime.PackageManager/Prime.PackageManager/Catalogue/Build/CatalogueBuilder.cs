﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Prime.Core;

namespace Prime.PackageManager
{
    public class CatalogueBuilder
    {
        private readonly ClientContext _context;

        public CatalogueBuilder(ClientContext context)
        {
            _context = context;
        }

        public Catalogue Build()
        {
            var dDir = _context.FileSystem.DistributionDirectory;
            var cDir = _context.FileSystem.CatalogueDirectory;

            var dirs = dDir.EnumerateDirectories().ToList();

            _context.L.Info("Scanning distribution directory: " + dDir.FullName);

            if (dirs.Count == 0)
            {
                _context.L.Warn("Distribution directory is empty.");
                return null;
            }

            var cat = Discover(dirs);

            _context.L.Info($"{cat.Count} entries found.");

            var jsonObject = CreateJsonObject(cat);

            var catJson = new FileInfo(Path.Combine(cDir.FullName, "cat.json"));

            if (catJson.Exists)
                catJson.Delete();

            File.WriteAllText(catJson.FullName, JsonConvert.SerializeObject(jsonObject, Formatting.Indented));

            _context.L.Info($"{catJson.FullName} catalogue written.");

            return jsonObject;
        }

        private CatalogueBuild Discover(List<DirectoryInfo> dirs)
        {
            var cat = new CatalogueBuild();
            foreach (var d in dirs)
            {
                foreach (var sd in d.GetDirectories())
                {
                    try
                    {
                        var entry = CataloguePackageBuilder.Rebuild(sd);
                        if (entry != null)
                            cat.Add(entry);
                    }
                    catch (Exception e)
                    {
                        _context.L.Error(e.Message + " in " + sd.FullName);
                    }
                }
            }

            return cat;
        }

        private Catalogue CreateJsonObject(CatalogueBuild catalogueBuild)
        {
            var cs = new Catalogue {Owner = "prime"};

            foreach (var group in catalogueBuild.GroupBy(x=>x.MetaData.Id).Where(x=>x.Any()))
            {
                var ces = new CataloguePackage() { Id = group.Key, Title = group.First().MetaData?.Title };
                cs.Entries.Add(ces);

                foreach (var i in group)
                    ces.Instances.Add(new CatalogueInstance(i.MetaData));
            }

            return cs;
        }
    }
}