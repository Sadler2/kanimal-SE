﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Text;
using NLog;

namespace kanimal
{
    public class KanimReader: Reader
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private Stream bild, anim;
        
        private Bitmap image;
        private Dictionary<string, int> AnimIdMap;

        public KanimReader(Stream bild, Stream anim, Stream img)
        {
            this.bild = bild;
            this.anim = anim;
            this.image = new Bitmap(img);
        }

        // Reads the entire build.bytes file
        public void ReadBuildData()
        {
            var reader = new BinaryReader(bild);
            
            try
            {
                VerifyHeader("BILD", reader);
            }
            catch (HeaderAssertException e)
            {
                Logger.Error(e);
                Logger.Error("Did you provide the right build.bytes file?");
                Environment.Exit((int)ExitCodes.IncorrectHeader);
            }
            
            ReadSymbols(reader);
            ReadBuildHashes(reader);
            BuildBuildTable(image.Width, image.Height);
            
            Utilities.LogDebug(Logger, BuildData);
            Utilities.LogDebug(Logger, BuildHashes);
            Utilities.LogDebug(Logger, BuildTable);
        }
        
        // Reads the symbols and frames
        private void ReadSymbols(BinaryReader reader)
        {
            KBuild.Build buildData = new KBuild.Build
            {
                Version = reader.ReadInt32(),
                SymbolCount = reader.ReadInt32(),
                FrameCount = reader.ReadInt32(),
                Name = reader.ReadPString(),
                Symbols = new List<KBuild.Symbol>()
            };

            for (int i = 0; i < buildData.SymbolCount; i++)
            {
                var symbol = new KBuild.Symbol
                {
                    Hash = reader.ReadInt32(),
                    Path = buildData.Version > 9 ? reader.ReadInt32() : 0,
                    Color = reader.ReadInt32(),
                    Flags = reader.ReadInt32(),
                    FrameCount = reader.ReadInt32(),
                    Frames = new List<KBuild.Frame>()
                };

                int time = 0;
                for (int j = 0; j < symbol.FrameCount; j++)
                {
                    var frame = new KBuild.Frame
                    {
                        SourceFrameNum = reader.ReadInt32(),
                        Duration = reader.ReadInt32(),
                        BuildImageIndex = reader.ReadInt32(),
                        PivotX = reader.ReadSingle(),
                        PivotY = reader.ReadSingle(),
                        PivotWidth = reader.ReadSingle(),
                        PivotHeight = reader.ReadSingle(),
                        X1 = reader.ReadSingle(),
                        Y1 = reader.ReadSingle(),
                        X2 = reader.ReadSingle(),
                        Y2 = reader.ReadSingle(),
                        Time = time
                    };
                    time += frame.Duration;
                    symbol.Frames.Add(frame);
                }

                buildData.Symbols.Add(symbol);

                BuildData = buildData;
            }
        }

        // Reads the hashes and related strings
        private void ReadBuildHashes(BinaryReader reader)
        {
            var buildHashes = new Dictionary<int, string>();
            var numHashes = reader.ReadInt32();
            for (int i = 0; i < numHashes; i++)
            {
                var hash = reader.ReadInt32();
                var str = reader.ReadPString();
                buildHashes[hash] = str;
            }

            BuildHashes = buildHashes;
        }

        // Unpacks the spritesheet into individual sprites and stores in memory.
        public void ExportTextures()
        {
            Sprites = new List<Sprite>();
            foreach (var row in BuildTable)
            {
                Logger.Debug($"{row.X1} {row.Height - row.Y1} {row.Width} {row.Height}    {image.Width} {image.Height}");
                var sprite = image.Clone(new Rectangle((int) row.X1, (int)(image.Height - row.Y1), (int)row.Width, (int)row.Height),
                    image.PixelFormat);
                Sprites.Add(new Sprite
                {
                    Name = $"{row.Name}_{row.Index}",
                    Bitmap = sprite
                });
            }
        }

        public void ReadAnimData()
        {
            var reader = new BinaryReader(anim);
            
            try
            {
                VerifyHeader("ANIM", reader);
            }
            catch (HeaderAssertException e)
            {
                Logger.Error(e);
                Logger.Error("Did you provide the right anim.bytes file?");
                Environment.Exit((int)ExitCodes.IncorrectHeader);
            }

            ParseAnims(reader);
            ReadAnimHashes(reader);
            ReadAnimIds();
            
            Utilities.LogDebug(Logger, AnimData);
            Utilities.LogDebug(Logger, AnimHashes);
            Utilities.LogDebug(Logger, AnimIdMap);
        }

        private void ParseAnims(BinaryReader reader)
        {
            var animData = new KAnim.Anim
            {
                Version = reader.ReadInt32(),
                ElementCount = reader.ReadInt32(),
                FrameCount = reader.ReadInt32(),
                AnimCount = reader.ReadInt32(),
                Anims = new List<KAnim.AnimBank>()
            };

            for (int i = 0; i < animData.AnimCount; i++)
            {
                var name = reader.ReadPString();
                var hash = reader.ReadInt32();
                Logger.Debug($"anim with name={name} but hash={hash}");
                var bank = new KAnim.AnimBank
                {
                    Name = name,
                    Hash = hash,
                    Rate = reader.ReadSingle(),
                    FrameCount = reader.ReadInt32(),
                    Frames = new List<KAnim.Frame>()
                };

                for (int j = 0; j < bank.FrameCount; j++)
                {
                    var frame = new KAnim.Frame
                    {
                        X = reader.ReadSingle(),
                        Y = reader.ReadSingle(),
                        Width = reader.ReadSingle(),
                        Height = reader.ReadSingle(),
                        ElementCount = reader.ReadInt32(),
                        Elements = new List<KAnim.Element>()
                    };
                    Logger.Debug($"animation frame=({frame.X},{frame.Y},{frame.Width},{frame.Height}");

                    for (int k = 0; k < frame.ElementCount; k++)
                    {
                        var element = new KAnim.Element
                        {
                            Image = reader.ReadInt32(),
                            Index = reader.ReadInt32(),
                            Layer = reader.ReadInt32(),
                            Flags = reader.ReadInt32(),
                            A = reader.ReadSingle(),
                            B = reader.ReadSingle(),
                            G = reader.ReadSingle(),
                            R = reader.ReadSingle(),
                            M1 = reader.ReadSingle(),
                            M2 = reader.ReadSingle(),
                            M3 = reader.ReadSingle(),
                            M4 = reader.ReadSingle(),
                            M5 = reader.ReadSingle(),
                            M6 = reader.ReadSingle(),
                            Order = reader.ReadSingle()
                        };
                        
                        Logger.Debug($"internal=({element.M5},{element.M6})");
                        Logger.Debug($"layer={element.Layer}");

                        frame.Elements.Add(element);
                    }
                    
                    Logger.Debug("");
                    bank.Frames.Add(frame);
                }

                animData.Anims.Add(bank);
            }

            animData.MaxVisibleSymbolFrames = reader.ReadInt32();

            AnimData = animData;
        }

        private void ReadAnimHashes(BinaryReader reader)
        {
            var animHashes = new Dictionary<int, string>();

            int numHashes = reader.ReadInt32();
            for (int i = 0; i < numHashes; i++)
            {
                var hash = reader.ReadInt32();
                var text = reader.ReadPString();
                animHashes[hash] = text;
            }

            AnimHashes = animHashes;
        }

        private void ReadAnimIds()
        {
            var animIdMap = new Dictionary<string, int>();

            var key = 0;
            foreach (var bank in AnimData.Anims)
            {
                foreach (var frame in bank.Frames)
                {
                    foreach (var element in frame.Elements)
                    {
                        var name = $"{AnimHashes[element.Image]}_{element.Index}_{AnimHashes[element.Layer]}";
                        if (!animIdMap.ContainsKey(name))
                        {
                            animIdMap[name] = key;
                            key += 1;
                        }
                    }
                }
            }

            AnimIdMap = animIdMap;
        }

        private void VerifyHeader(string expectedHeader, BinaryReader buffer)
        {
            var actualHeader = Encoding.ASCII.GetString(buffer.ReadBytes(expectedHeader.Length));

            if (expectedHeader != actualHeader)
            {
                throw new HeaderAssertException(
                    $"Expected header \"{expectedHeader}\" but got \"{actualHeader}\" instead.",
                    expectedHeader,
                    actualHeader);
            }
        }

        // TODO: Include images into the in-memory format & remove outputdir arg for something better
        public override void Read(string outputDir)
        {
            Logger.Info("Parsing build data.");
            ReadBuildData();
            
            Logger.Info("Importing textures.");
            ExportTextures();
            
            Logger.Info("Parsing animation data.");
            ReadAnimData();
        }

        public override Bitmap GetSpriteSheet()
        {
            return image;
        }
    }
}