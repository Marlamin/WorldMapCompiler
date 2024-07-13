using CASCLib;
using DBCD.Providers;
using System;
using System.IO;

namespace WorldMapCompiler
{
    class CASCDBCProvider : IDBCProvider
    {
        private static CASCHandler CASC;

        public CASCDBCProvider(CASCHandler casc)
        {
            CASC = casc;
        }

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

            if (CASC.FileExists((int)fileDataID))
            {
                return CASC.OpenFile((int)fileDataID);
            }
            else
            {
                throw new FileNotFoundException("Could not find " + fileDataID);
            }
        }
    }
}