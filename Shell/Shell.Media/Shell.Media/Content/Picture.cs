﻿using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shell.Common.IO;
using Shell.Common.Util;
using Shell.Media.Files;
using System.Drawing.Imaging;
using System.Drawing;
using System.Linq;
using System.Globalization;

namespace Shell.Media.Content
{
    public class Picture : Medium
    {
        public static HashSet<string> FILE_ENDINGS = new [] {
            ".png",
            ".jpg",
            ".gif",
            ".jpeg",
            ".xcf",
            ".bmp",
            ".tiff",
            ".tif",
            ".ico",
            ".pamp",
        }.ToHashSet ();

        public static Dictionary<string[],string[]> MIME_TYPES = new Dictionary<string[],string[]> () {
            { new [] { "image/jpeg" }, new [] { ".jpg", ".jpeg", ".pamp" } },
            { new [] { "image/png" }, new [] { ".png" } },
            { new [] { "image/gif" }, new [] { ".gif" } },
            { new [] { "image/svg+xml" }, new [] { ".svg" } },
            { new [] { "image/tiff" }, new [] { ".tif", ".tiff" } },
            { new [] { "image/x-ms-bmp" }, new [] { ".bmp" } },
            { new [] { "image/x-icon", "image/vnd.microsoft.icon" }, new [] { ".ico" } },
            { new [] { "image/x-xcf" }, new [] { ".xcf" } },
        };

        public static readonly string TYPE = "picture";

        private static PictureLibrary lib = new PictureLibrary ();

        public override string Type { get { return TYPE; } }

        public List<ExifTag> ExifTags = new List<ExifTag> ();

        public DateTime? ExifTimestampCreated {
            get {
                string[] possibleTagNames = new [] {
                    "DateTimeOriginal",
                    "CreateDate",
                    "GPSDateTime",
                    //"ModifyDate",
                    "DateTime",
                };
                return lib.TryParseExifTimestamp (exifTags: ExifTags, possibleTagNames: possibleTagNames);
            }
        }

        public DateTime? ExifTimestampModified {
            get {
                string[] possibleTagNames = new [] {
                    "ModifyDate",
                };
                return lib.TryParseExifTimestamp (exifTags: ExifTags, possibleTagNames: possibleTagNames);
            }
        }

        public DateTime? ExifTimestampAcquired {
            get {
                string[] possibleTagNames = new [] {
                    "DateAcquired",
                };
                return lib.TryParseExifTimestamp (exifTags: ExifTags, possibleTagNames: possibleTagNames);
            }
        }

        public override DateTime? PreferredTimestamp {
            get {
                DateTime? preferred = null;
                if (ExifTimestampCreated.HasValue)
                    preferred = ExifTimestampCreated;
                else if (ExifTimestampModified.HasValue)
                    preferred = ExifTimestampModified;
                else if (ExifTimestampAcquired.HasValue)
                    preferred = ExifTimestampAcquired;
                   
                return preferred;
            }
        }

        public bool IsDateless { get; private set; }

        public bool IsCommonFormat { get; private set; }

        public HexString PixelHash { get; private set; }

        public Picture (HexString hash)
            : base (hash)
        {
            IsDateless = false;
        }

        public static bool IsValidFile (string fullPath)
        {
            return MediaShareUtilities.IsValidFile (fullPath: fullPath, fileEndings: FILE_ENDINGS);
        }

        public override void Index (string fullPath)
        {
            if (string.IsNullOrWhiteSpace (MimeType)) {
                MimeType = libMediaFile.GetMimeTypeByExtension (fullPath: fullPath);
            }
            IsCommonFormat = MimeType != "image/x-xcf";

            if (ExifTags.Count == 0) {
                Log.Debug ("Index: ", fullPath);
                ExifTags = lib.GetExifTags (fullPath: fullPath);
            }

            if (ExifTimestampCreated == null && ExifTimestampModified == null && ExifTimestampAcquired == null) {
                string fileName = Path.GetFileName (fullPath);
                DateTime date;
                if (NamingUtilities.GetFileNameDate (fileName: fileName, date: out date)) {
                    Log.Message ("Index: Set exif date for picture: ", fullPath, " => ", string.Format ("{0:yyyy:MM:dd HH:mm:ss}", date));
                    lib.SetExifDate (fullPath: fullPath, date: date);
                    ExifTags = lib.GetExifTags (fullPath: fullPath);
                    IsDateless = false;
                } else {
                    IsDateless = true;
                }
            } else {
                IsDateless = false;
            }

            if (IsCommonFormat && string.IsNullOrWhiteSpace (PixelHash.Hash)) {
                Log.Debug ("Index: ", fullPath);
                Bitmap bitmap = lib.ReadBitmap (fileName: fullPath);
                if (bitmap != null) {
                    using (bitmap) {
                        if (PixelHash.Hash == null) {
                            var result = lib.GetPixelHash (bitmap: bitmap);
                            if (result.HasValue) {
                                PixelHash = result.Value;
                            } else {
                                Log.Error ("Index: Unable to get pixel hash! fullPath=", fullPath);
                            }
                            Log.Message ("PixelHash: ", PixelHash);
                        }
                    }
                } else {
                    Log.Error ("Error! Index Picture: Can't read bitmap: ", fullPath);
                    IsDateless = true;
                }
            }
        }

        public override bool IsCompletelyIndexed {
            get {
                return (ExifTags.Count > 0 || IsDateless)
                && (!string.IsNullOrWhiteSpace (PixelHash.Hash) || !IsCommonFormat)
                && !string.IsNullOrWhiteSpace (MimeType);
            }
        }

        public static void RunIndexHooks (ref string fullPath)
        {
            // is the file ending in BMP format?
            if (Path.GetExtension (fullPath) == ".bmp") {
                Picture.ConvertToJpeg (fullPath: ref fullPath);
            }
            // WTF is pamp?
            if (Path.GetExtension (fullPath) == ".pamp") {
                Picture.ConvertToJpeg (fullPath: ref fullPath);
            }
        }

        public static bool ConvertToJpeg (ref string fullPath)
        {
            string oldPath = fullPath;
            string newPath = Path.GetDirectoryName (oldPath) + SystemInfo.PathSeparator + Path.GetFileNameWithoutExtension (oldPath) + ".jpg";

            try {
                Log.Message ("Convert picture to JPEG: ", Path.GetFileName (oldPath), " => ", Path.GetFileName (newPath));
                Image original = Image.FromFile (oldPath);
                EncoderParameters encoderParams = new EncoderParameters (1);
                encoderParams.Param [0] = new EncoderParameter (System.Drawing.Imaging.Encoder.Quality, 100L);
                original.Save (filename: newPath, encoder: PictureLibrary.GetEncoder (ImageFormat.Jpeg), encoderParams: encoderParams);
                lib.CopyExifTags (sourcePath: oldPath, destPath: newPath);
                if (File.Exists (newPath) && File.Exists (oldPath)) {
                    File.Delete (oldPath);
                    fullPath = newPath;
                }
                return true;
            } catch (Exception ex) {
                Log.Error (ex);
            }
            return false;
        }

        protected override void SerializeInternal (Dictionary<string, string> dict)
        {
            // exif tags
            foreach (ExifTag tag in ExifTags) {
                string key;
                string value;
                if (tag.Serialize (out key, out value)) {
                    dict [key] = value;
                }
            }

            // is dateless?
            dict ["flag:IsDateless"] = IsDateless ? "true" : "false";

            // is in a common format?
            dict ["flag:IsCommonFormat"] = IsCommonFormat ? "true" : "false";

            // save the pixel hash
            dict ["picture:PixelHash"] = PixelHash.Hash;
        }

        protected override void DeserializeInternal (Dictionary<string, string> dict)
        {
            // exif tags
            ExifTags.Clear ();
            foreach (string key in dict.Keys) {
                string value = dict [key];
                ExifTag deserialized = null;
                if (ExifTag.Deserialize (key, value, out deserialized)) {
                    ExifTags.Add (deserialized);
                }
            }

            // is dateless?
            IsDateless = dict.ContainsKey ("flag:IsDateless") ? (dict ["flag:IsDateless"] == "true") : false;

            // is in a common format?
            IsCommonFormat = dict.ContainsKey ("flag:IsCommonFormat") ? (dict ["flag:IsCommonFormat"] == "true") : true;

            // load the pixel hash
            PixelHash = new HexString { Hash = dict ["picture:PixelHash"] };
        }
    }
}
