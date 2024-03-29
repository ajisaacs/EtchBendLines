﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace EtchBendLines
{
    public class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var etcher = new Etcher();
                etcher.EtchLength = GetAppSettingAsDouble("EtchLength");
                etcher.MaxBendRadius = GetAppSettingAsDouble("MaxBendRadius");

                var paths = new List<string>(args);

                if (paths.Count == 0)
                {
                    paths.Add(AppDomain.CurrentDomain.BaseDirectory);
                }

                var files = new List<string>();

                foreach (var path in paths)
                {
                    if (File.Exists(path))
                    {
                        files.Add(path);
                    }
                    else if (Directory.Exists(path))
                    {
                        files.AddRange(Directory.GetFiles(path, "*.dxf", SearchOption.AllDirectories));
                    }
                }

                if (files.Count == 0)
                {
                    Console.WriteLine($"No DXF files founds. Place DXF files in \"{AppDomain.CurrentDomain.BaseDirectory}\" and run this program again.");
                }
                else
                {
                    foreach (var file in files)
                    {
                        etcher.AddEtchLines(file);
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"An error occured: {ex.Message}");
                Console.ResetColor();
            }

            PressAnyKeyToExit();
        }

        static double GetAppSettingAsDouble(string name)
        {
            double v;

            try
            {
                return double.Parse(ConfigurationManager.AppSettings[name]);
            }
            catch
            {
               throw new Exception($"Failed to convert the value of AppSetting[\"{name}\"] to double");
            }
        }

        static void PressAnyKeyToExit()
        {
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

        static PartType GetPartType(List<Bend> bends)
        {
            if (bends.Count == 0)
                return PartType.Flat;

            var upBends = bends.Where(b => b.Direction == BendDirection.Up).ToList();
            var downBends = bends.Where(b => b.Direction == BendDirection.Down).ToList();

            if (upBends.Count == 0 || downBends.Count == 0)
            {
                // bends are going the same direction

                if (bends.Count == 2 && bends[0].IsParallelTo(bends[1]))
                    return PartType.Channel;

                if (bends.Count == 4)
                {
                    var groups = bends.GroupBy(b => b.Line.Slope()).ToList();

                    if (groups.Count == 2)
                    {
                        var bend1 = groups[0].First();
                        var bend2 = groups[1].First();

                        if (bend1.IsPerpendicularTo(bend2))
                        {
                            return PartType.Pan;
                        }
                    }
                }
            }

            if (bends.Count == 1)
                return PartType.Angle;

            if (bends.Count == 2)
            {
                var bend1 = bends[0];
                var bend2 = bends[1];

                if (bend1.IsParallelTo(bend2))
                    return PartType.ZAngle;
            }

            return PartType.Other;
        }
    }

    public enum PartType
    {
        Flat,
        Angle,
        Channel,
        Pan,
        ZAngle,
        Other
    }
}
