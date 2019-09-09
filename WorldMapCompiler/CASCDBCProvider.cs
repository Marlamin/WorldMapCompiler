using DBCD.Providers;
using System;
using System.IO;
using WoWFormatLib.Utils;

namespace WorldMapCompiler
{
    class CASCDBCProvider : IDBCProvider
    {
        public Stream StreamForTableName(string tableName, string build)
        {
            uint fileDataID = 0;

            switch (tableName)
            {
                case "WorldMapOverlay":
                    fileDataID = 1134579;
                    break;
                case "WorldMapOverlayTile":
                    fileDataID = 1957212;
                    break;
                case "UiMapArt":
                    fileDataID = 1957202;
                    break;
                case "UiMap":
                    fileDataID = 1957206;
                    break;
                case "UiMapArtStyleLayer":
                    fileDataID = 1957208;
                    break;
                case "UiMapArtTile":
                    fileDataID = 1957210;
                    break;
                case "UiMapXMapArt":
                    fileDataID = 1957217;
                    break;
                default:
                    throw new Exception("Unable to find FileDataID for DBC " + tableName);
            }

            if (CASC.FileExists(fileDataID))
            {
                return CASC.OpenFile(fileDataID);
            }
            else
            {
                throw new FileNotFoundException("Could not find " + fileDataID);
            }
        }
    }
}