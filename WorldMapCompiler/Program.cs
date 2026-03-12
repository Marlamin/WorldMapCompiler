using BLPSharp;
using CASCLib;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace WorldMapCompiler;

internal class Program
{
    private static readonly GraphicsOptions Options = new()
    {
        AlphaCompositionMode = PixelAlphaCompositionMode.SrcOver, // SrcOver (default), SrcAtop, etc.
        ColorBlendingMode = PixelColorBlendingMode.Normal, // Normal, Multiply, Screen, etc.
        // Alpha = 1f // Opacity (1f for fully opaque)
    };

    private static readonly PngEncoder Encoder = new()
    {
        // Level6 = 3-4% smaller files than Level5 at more than double the encode time
        CompressionLevel = PngCompressionLevel.Level5,
        FilterMethod = PngFilterMethod.Adaptive,
    };

    private static void Main(string[] args)
    {
        var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("settings.json", true, true).Build();

        var saveExplored = bool.Parse(config["saveExploredMaps"]);
        var saveUnexplored = bool.Parse(config["saveUnexploredMaps"]);
        var saveLayers = bool.Parse(config["saveMapLayers"]);
        var saveExploredMapsWithoutUnexplored = bool.Parse(config["saveExploredMapsWithoutUnexplored"]);

        if (saveExplored && !Directory.Exists("explored"))
        {
            Directory.CreateDirectory("explored");
        }

        if (saveUnexplored && !Directory.Exists("unexplored"))
        {
            Directory.CreateDirectory("unexplored");
        }

        if (saveLayers && !Directory.Exists("layers"))
        {
            Directory.CreateDirectory("layers");
        }

        if (saveExploredMapsWithoutUnexplored && !Directory.Exists("exploredNoUnexplored"))
        {
            Directory.CreateDirectory("exploredNoUnexplored");
        }

        var locale = LocaleFlags.enUS;

        if (config["locale"] != string.Empty)
        {
            switch (config["locale"])
            {
                case "deDE":
                    locale = LocaleFlags.deDE;
                    break;
                case "enUS":
                    locale = LocaleFlags.enUS;
                    break;
                case "ruRU":
                    locale = LocaleFlags.ruRU;
                    break;
                case "zhCN":
                    locale = LocaleFlags.zhCN;
                    break;
                case "zhTW":
                    locale = LocaleFlags.zhTW;
                    break;
                case "frFR":
                    locale = LocaleFlags.frFR;
                    break;
            }
        }

        CASCHandler cascHandler;
        if (config["installDir"] != string.Empty && Directory.Exists(config["installDir"]))
        {
            cascHandler = CASCHandler.OpenLocalStorage(config["installDir"], config["program"]);
        }
        else
        {
            cascHandler = CASCHandler.OpenOnlineStorage(config["program"]);
        }

        cascHandler.Root.SetFlags(locale);

        var dbcd = new DBCD.DBCD(new CASCDBCProvider(cascHandler), new DBCD.Providers.GithubDBDProvider());

        string build;

        if (cascHandler.Config.BuildName.StartsWith("WOW-"))
        {
            var buildNumber = cascHandler.Config.BuildName.Split("patch")[0].Replace("WOW-", "");
            var buildName = cascHandler.Config.BuildName.Split("patch")[1].Split('_')[0];
            build = buildName + "." + buildNumber;
        }
        else
        {
            build = cascHandler.Config.BuildName;
        }

        var UIMap = dbcd.Load("UiMap", build);
        var UIMapXArt = dbcd.Load("UiMapXMapArt", build);
        var UIMapArtTile = dbcd.Load("UiMapArtTile", build);
        var UIMapArt = dbcd.Load("UiMapArt", build);
        var UIMapArtStyleLayer = dbcd.Load("UiMapArtStyleLayer", build);
        var WorldMapOverlay = dbcd.Load("WorldMapOverlay", build);
        var WorldMapOverlayTile = dbcd.Load("WorldMapOverlayTile", build);

        Console.WriteLine(); // new line after wdc2 debug output

        foreach (dynamic mapRow in UIMap.Values.Reverse())
        {
            var mapName = mapRow.Name_lang;
            var cleanMapName = CleanFileName($"{mapRow.ID} - {mapName}");

            Console.WriteLine(mapRow.ID + " = " + mapName);

            foreach (dynamic mxaRow in UIMapXArt.Values)
            {
                var uiMapArtID = mxaRow.UiMapArtID;
                var uiMapID = mxaRow.UiMapID;

                if (mxaRow.PhaseID != 0)
                    continue; // Skip phase stuff for now

                if (uiMapID != mapRow.ID)
                    continue;

                var maxRows = uint.MinValue;
                var maxCols = uint.MinValue;
                var tileDict = new Dictionary<string, int>();

                foreach (dynamic matRow in UIMapArtTile.Values)
                {
                    var matUiMapArtID = matRow.UiMapArtID;
                    if (matUiMapArtID == uiMapArtID)
                    {
                        var fdid = matRow.FileDataID;
                        var rowIndex = matRow.RowIndex;
                        var colIndex = matRow.ColIndex;
                        var layerIndex = matRow.LayerIndex;

                        // Skip other layers for now
                        if (layerIndex != 0)
                        {
                            continue;
                        }

                        if (rowIndex > maxRows)
                        {
                            maxRows = rowIndex;
                        }

                        if (colIndex > maxCols)
                        {
                            maxCols = colIndex;
                        }

                        tileDict.Add(rowIndex + "," + colIndex, fdid);
                    }
                }

                uint res_x = 0;
                uint res_y = 0;

                foreach (dynamic maRow in UIMapArt.Values)
                {
                    if (maRow.ID == uiMapArtID)
                    {
                        foreach (dynamic mastRow in UIMapArtStyleLayer.Values)
                        {
                            if (mastRow.ID == maRow.UiMapArtStyleID)
                            {
                                res_x = mastRow.LayerHeight;
                                res_y = mastRow.LayerWidth;
                                continue;
                            }
                        }

                        continue;
                    }
                }

                if (res_x == 0)
                {
                    res_x = (maxRows + 1) * 256;
                }

                if (res_y == 0)
                {
                    res_y = (maxCols + 1) * 256;
                }

                Image<Bgra32> image1 = new((int)res_y, (int)res_x);

                for (var cur_x = 0; cur_x < maxRows + 1; cur_x++)
                {
                    for (var cur_y = 0; cur_y < maxCols + 1; cur_y++)
                    {
                        var fdid = tileDict[cur_x + "," + cur_y];

                        if (cascHandler.FileExists(fdid))
                        {
                            try
                            {
                                using (var stream = cascHandler.OpenFile(fdid))
                                {
                                    var layer = LoadBlp(stream);
                                    layer.Mutate(ctx => ctx.Resize(256, 256));
                                    image1.Mutate(ctx =>
                                        ctx.DrawImage(layer, new Point(cur_y * 256, cur_x * 256), Options));
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("An error occured opening BLP with filedataid " + fdid + ": " +
                                                  e.Message);
                            }
                        }
                    }
                }

                if (saveUnexplored && image1 != null)
                {
                    SaveImage(image1, Path.Join("unexplored", $"{cleanMapName}.png"));
                }

                if (!saveLayers && !saveExplored)
                {
                    continue;
                }

                Image<Bgra32> image2 = new((int)res_x, (int)res_y);

                foreach (dynamic wmorow in WorldMapOverlay.Values)
                {
                    var WMOUIMapArtID = wmorow.UiMapArtID;
                    var offsetX = wmorow.OffsetX;
                    var offsetY = wmorow.OffsetY;

                    uint maxWMORows = 0;
                    uint maxWMOCols = 0;
                    var wmoTileDict = new Dictionary<string, int>();

                    if (WMOUIMapArtID == uiMapArtID)
                    {
                        foreach (dynamic wmotrow in WorldMapOverlayTile.Values)
                        {
                            var worldMapOverlayID = wmotrow.WorldMapOverlayID;

                            // something wrong in/around this check
                            if (worldMapOverlayID == wmorow.ID)
                            {
                                var fdid = wmotrow.FileDataID;
                                var rowIndex = wmotrow.RowIndex;
                                var colIndex = wmotrow.ColIndex;
                                var layerIndex = wmotrow.LayerIndex;

                                // Skip other layers for now
                                if (layerIndex != 0)
                                {
                                    continue;
                                }

                                if (rowIndex > maxWMORows)
                                {
                                    maxWMORows = rowIndex;
                                }

                                if (colIndex > maxWMOCols)
                                {
                                    maxWMOCols = colIndex;
                                }

                                wmoTileDict.Add(rowIndex + "," + colIndex, fdid);
                            }
                        }
                    }

                    if (wmoTileDict.Count == 0)
                    {
                        continue;
                    }

                    var layerResX = (maxWMORows + 1) * 256;
                    var layerResY = (maxWMOCols + 1) * 256;

                    var layerImage = new Image<Bgra32>((int)layerResY, (int)layerResX);

                    for (var cur_x = 0; cur_x < maxWMORows + 1; cur_x++)
                    {
                        for (var cur_y = 0; cur_y < maxWMOCols + 1; cur_y++)
                        {
                            var fdid = wmoTileDict[cur_x + "," + cur_y];

                            if (!cascHandler.FileExists(fdid))
                            {
                                continue;
                            }

                            try
                            {
                                using (var stream = cascHandler.OpenFile(fdid))
                                {
                                    var layer = LoadBlp(stream);
                                    layer.Mutate(ctx => ctx.Resize(256, 256));

                                    var posY = cur_y * 256 + offsetX;
                                    var posX = cur_x * 256 + offsetY;

                                    if (saveLayers)
                                    {
                                        layerImage.Mutate(ctx =>
                                            ctx.DrawImage(layer, new Point(cur_y * 256, cur_x * 256), Options));
                                    }

                                    image1.Mutate(ctx => ctx.DrawImage(layer, new Point(posY, posX), Options));

                                    if (saveExploredMapsWithoutUnexplored)
                                    {
                                        image2.Mutate(ctx =>
                                            ctx.DrawImage(layer, new Point(posY, posX), Options));
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("An error occured opening BLP with filedataid " + fdid);
                            }
                        }
                    }

                    if (saveLayers)
                    {
                        string layersPath = Path.Join("layers", cleanMapName);
                        if (!Directory.Exists(layersPath))
                        {
                            Directory.CreateDirectory(layersPath);
                        }

                        SaveImage(layerImage, Path.Join("layers", cleanMapName, $"{wmorow.ID}.png"));
                    }
                }

                if (saveExplored)
                {
                    SaveImage(image1, Path.Join("explored", $"{cleanMapName}.png"));
                }

                if (saveExploredMapsWithoutUnexplored)
                {
                    SaveImage(image2, Path.Join("exploredNoUnexplored", $"{cleanMapName}.png"));
                }
            }
        }
    }

    private static string CleanFileName(string fileName)
    {
        return Path.GetInvalidFileNameChars()
            .Aggregate(fileName, (current, c) => current.Replace(c.ToString(), string.Empty));
    }

    private static Image<Bgra32> LoadBlp(Stream stream)
    {
        var blp = new BLPFile(stream);
        var pixels = blp.GetPixels(1, out int width, out int height);
        return Image.LoadPixelData<Bgra32>(pixels, width, height);
    }
    
    private static void SaveImage(Image image, string filename)
    {
        image.Save(filename, Encoder);
    }
}
