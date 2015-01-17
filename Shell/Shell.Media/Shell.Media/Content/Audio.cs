﻿using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shell.Common.Util;
using Shell.Media.Files;

namespace Shell.Media.Content
{
    public class Audio : Medium
    {
        public static HashSet<string> FILE_ENDINGS = new []{ ".mp3", ".wav", ".ogg", ".aac", ".m4a" }.ToHashSet ();

        public static Dictionary<string[],string[]> MIME_TYPES = new Dictionary<string[],string[]> () {
            { new [] { "audio/mpeg" }, new [] { ".mp3" } },
            { new [] { "audio/x-wav" }, new [] { ".wav" } },
            { new [] { "audio/ogg" }, new [] { ".ogg" } },
            { new [] { "audio/x-ms-wma" }, new [] { ".wma" } },
            { new [] { "audio/flac" }, new [] { ".flac" } }
        };

        public static readonly string TYPE = "audio";

        public override string Type { get { return TYPE; } }

        public override DateTime? PreferredTimestamp { get { return null; } }

        public Audio (HexString hash)
            : base (hash)
        {
        }

        public static bool IsValidFile (string fullPath)
        {
            return MediaShareUtilities.IsValidFile (fullPath: fullPath, fileEndings: FILE_ENDINGS);
        }

        public override void Index (string fullPath)
        {
        }

        public override bool IsCompletelyIndexed {
            get {
                return true;
            }
        }

        public static void RunIndexHooks (ref string fullPath)
        {
        }

        protected override void SerializeInternal (Dictionary<string, string> dict)
        {
        }

        protected override void DeserializeInternal (Dictionary<string, string> dict)
        {
        }
    }
}

