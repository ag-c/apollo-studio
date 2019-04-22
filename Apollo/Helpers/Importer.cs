using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using Newtonsoft.Json;

using Apollo.Core;
using Apollo.Elements;
using Apollo.Helpers;
using Apollo.Structures;

namespace Apollo.Helpers {
    public class Importer {
        private static int MIDIReadVariableLength(BinaryReader reader) {
            int ret = 0;
            for (int i = 0; i < 4; i++) {
                byte b = reader.ReadByte();
                ret <<= 7;
                ret += (b & 0x7F);
                if (b >> 7 == 0) return ret;
            }
            return ret;
        }

        private static void MIDIDiscardMeta(BinaryReader reader) {
            switch (reader.ReadByte()) {
                case 0x00: // Sequence number
                case 0x59: // Key signature
                    reader.ReadBytes(3);
                    break;

                case 0x01: // Text event
                case 0x02: // Copyright notice
                case 0x03: // Track name
                case 0x04: // Instrument name
                case 0x05: // Lyric
                case 0x06: // Marker
                case 0x07: // Cue Point
                case 0x7F: // Sequencer specific
                    reader.ReadBytes(MIDIReadVariableLength(reader));
                    break;

                case 0x20: // MIDI channel prefix
                case 0x21: // MIDI Port
                    reader.ReadBytes(2);
                    break;
                
                case 0x2F: // End of Track
                    reader.ReadByte();
                    break;
                
                case 0x51: // Tempo
                    reader.ReadBytes(4);
                    break;
                
                case 0x54: // SMPTE Offset
                    reader.ReadBytes(6);
                    break;
                
                case 0x58: // Time signature
                    reader.ReadBytes(5);
                    break;
            }
        }
        
        public static bool FramesFromMIDI(string path, out List<Frame> ret) {
            ret = null;

            if (!File.Exists(path)) return false;
            
            using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open))) {
                if (!reader.ReadChars(4).SequenceEqual(new char[] {'M', 'T', 'h', 'd'}) || // Header Identifier
                    !reader.ReadBytes(4).SequenceEqual(new byte[] {0x00, 0x00, 0x00, 0x06}) || // Header size
                    !reader.ReadBytes(4).SequenceEqual(new byte[] {0x00, 0x00, 0x00, 0x01})) // Single track file
                    return false;
                
                reader.ReadBytes(2); // Skip BPM info (usually 0x00, 0x60 = 120BPM)

                if (!reader.ReadChars(4).SequenceEqual(new char[] {'M', 'T', 'r', 'k'})) // Track start
                    return false;
                
                long end = reader.BaseStream.Position + BitConverter.ToInt32(reader.ReadBytes(4).Reverse().ToArray()); // Track length
                ret = new List<Frame>();

                int time = 0;

                while (reader.BaseStream.Position < end) {
                    int delta = MIDIReadVariableLength(reader);
                    time += delta;

                    if (delta > 0) ret.Add(new Frame());
                    
                    byte type = reader.ReadByte();

                    switch (type >> 4) {                        
                        case 0x9: // Note on
                            ret.Last().Screen[reader.ReadByte()] = new Color((byte)(reader.ReadByte() >> 1));
                            break;
                        
                        case 0x7: // Channel Mode
                        case 0x8: // Note off
                        case 0xA: // Poly Aftertouch
                        case 0xB: // CC
                        case 0xE: // Pitch Wheel
                            reader.ReadBytes(2);
                            break;
                        
                        case 0xC: // Program Change
                        case 0xD: // Channel Aftertouch
                            reader.ReadByte();
                            break;

                        case 0xF: // System Common
                            switch ((byte)(type & 0x0F)) {
                                case 0x0: // SysEx Start
                                    reader.ReadBytes(MIDIReadVariableLength(reader));
                                    break;
                                
                                case 0x2: // Song position pointer
                                    reader.ReadBytes(2);
                                    break;

                                case 0x3: // Song select
                                    reader.ReadByte();
                                    break;
                                
                                case 0xF: // Meta
                                    MIDIDiscardMeta(reader);
                                    break;
                            }
                            break;
                    }
                }

                return reader.BaseStream.Position == end;
            }
        }
    }
}