namespace ExtractBundler;

using System;
using System.Collections.Generic;
using System.IO;

public enum Identifier
{
    Full,
    StreetName,
    Address,
    AddressLinks
}

public enum ZipKey
{
    AzureZip,
    AzureGeoPackageZip,
    GeoPackage,
    S3Zip,
    AzureZipRootDir,
    MetaGrarXml,
    MetaGrarPdf,
    InstructionPdf,
    MetadataUpdatedMessage,
    MetadataUpdatedMessageFailed,
    ExtractDoneMessage
}

public static class ZipKeys
{
    private static Dictionary<ZipKey, string> Full => new()
    {
        { ZipKey.ExtractDoneMessage, "Full extract DONE." },
        { ZipKey.MetadataUpdatedMessage, "Metadata Updated for Full extract." },
        { ZipKey.MetadataUpdatedMessageFailed, "Failed Update Metadata Center for Full extract." },
        { ZipKey.AzureZip, "GRAR.zip" },
        { ZipKey.AzureGeoPackageZip, "GRAR_Gpkg.zip" },
        { ZipKey.GeoPackage, "GRAR.gpkg" },
        { ZipKey.S3Zip, $"{DateTime.Today.ToString("yyyyMMdd")}-Downloadbestand-Gebouwen-Adressenregister.zip" },
        { ZipKey.AzureZipRootDir, $"{DateTime.Today.ToString("yyyyMMdd")}_GRAR_Data" },
        { ZipKey.MetaGrarXml , "Meta_GRAR.xml"},
        { ZipKey.MetaGrarPdf , "Meta_GRAR.pdf"},
        { ZipKey.InstructionPdf , "HandleidingZipPakketten.pdf"}
    };

    private static Dictionary<ZipKey, string> StreetName => new()
    {
        { ZipKey.ExtractDoneMessage, "StreetName extract DONE." },
        { ZipKey.MetadataUpdatedMessage, "Metadata Updated for StreetName extract." },
        { ZipKey.MetadataUpdatedMessageFailed, "Failed Update Metadata Center for StreetName extract." },
        { ZipKey.AzureZip, "GRAR_Straatnamen.zip" },
        { ZipKey.AzureGeoPackageZip, "GRAR_Straatnamen_Gpkg.zip" },
        { ZipKey.GeoPackage, "GRAR_Straatnamen.gpkg" },
        { ZipKey.S3Zip, $"{DateTime.Today.ToString("yyyyMMdd")}-Downloadbestand-Gebouwen-Adressenregister_straatnamen.zip" },
        { ZipKey.AzureZipRootDir, $"{DateTime.Today.ToString("yyyyMMdd")}_GRAR_Straatnamen_Data" },
        { ZipKey.MetaGrarXml , "Meta_GRARStraatnamen.xml"},
        { ZipKey.MetaGrarPdf , "Meta_GRARStraatnamen.pdf"},
        { ZipKey.InstructionPdf , "HandleidingZipPakketten.pdf"}
    };

    private static Dictionary<ZipKey, string> Address => new()
    {
        { ZipKey.ExtractDoneMessage, "Address extract DONE." },
        { ZipKey.MetadataUpdatedMessage, "Metadata Updated for Address extract." },
        { ZipKey.MetadataUpdatedMessageFailed, "Failed Update Metadata Center for Address extract." },
        { ZipKey.AzureZip, "GRAR_Adressen.zip" },
        { ZipKey.AzureGeoPackageZip, "GRAR_Adressen_Gpkg.zip" },
        { ZipKey.GeoPackage, "GRAR_Adressen.gpkg" },
        { ZipKey.S3Zip, $"{DateTime.Today.ToString("yyyyMMdd")}-Downloadbestand-Gebouwen-Adressenregister_adressen.zip" },
        { ZipKey.AzureZipRootDir, $"{DateTime.Today.ToString("yyyyMMdd")}_GRAR_Adressen_Data" },
        { ZipKey.MetaGrarXml , "Meta_GRARAdressen.xml"},
        { ZipKey.MetaGrarPdf , "Meta_GRARAdressen.pdf"},
        { ZipKey.InstructionPdf , "HandleidingZipPakketten.pdf"}
    };

    private static Dictionary<ZipKey, string> AddressLinks => new()
    {
        { ZipKey.ExtractDoneMessage, "AddressLinks extract DONE." },
        { ZipKey.MetadataUpdatedMessage, "Metadata Updated for AddressLinks extract." },
        { ZipKey.MetadataUpdatedMessageFailed, "Failed Update Metadata Center for AddressLinks extract." },
        { ZipKey.AzureZip, "GRAR_Adreskoppelingen.zip" },
        { ZipKey.AzureGeoPackageZip, "GRAR_Adreskoppelingen_Gpkg.zip" },
        { ZipKey.GeoPackage, "GRAR_Adreskoppelingen.gpkg" },
        { ZipKey.S3Zip, $"{DateTime.Today.ToString("yyyyMMdd")}-Downloadbestand-Gebouwen-Adressenregister_adreskoppelingen.zip" },
        { ZipKey.AzureZipRootDir, $"{DateTime.Today.ToString("yyyyMMdd")}_GRAR_Adreskoppelingen_Data" },
        { ZipKey.MetaGrarXml , "Meta_GRARAdreskoppelingen.xml"},
        { ZipKey.MetaGrarPdf , "Meta_GRARAdreskoppelingen.pdf"},
        { ZipKey.InstructionPdf , "HandleidingZipPakketten.pdf"}
    };


    public static string GetValue(this Identifier identifier, ZipKey key)
    {
        if (identifier == Identifier.Full)
        {
            return Full[key];
        }

        if (identifier == Identifier.StreetName)
        {
            return StreetName[key];
        }

        if (identifier == Identifier.Address)
        {
            return Address[key];
        }

        if (identifier == Identifier.AddressLinks)
        {
            return AddressLinks[key];
        }

        return String.Empty;
    }

    public static string RewriteZipEntryFullNameForAzure(this Identifier identifier, string fileName)
    {
        bool addToShapeFileDir =
            fileName.EndsWith(".prj") ||
            fileName.EndsWith(".shp") ||
            fileName.EndsWith(".shx") ||
            fileName == "Adres.dbf" ||
            fileName == "Gebouw.dbf" ||
            fileName == "Gebouweenheid.dbf" ||
            fileName == "Adres_metadata.dbf" ||
            fileName == "Gebouw_metadata.dbf" ||
            fileName == "Gebouweenheid_metadata.dbf";

        var dirName = addToShapeFileDir ? "Shapefile" : "dBASE";
        return Path.Join($"{identifier.GetValue(ZipKey.AzureZipRootDir)}", dirName, fileName);
    }
}
