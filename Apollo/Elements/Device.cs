using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;

using Apollo.Structures;
using Apollo.Viewers;

namespace Apollo.Elements {
    public abstract class Device {
        public static readonly string Identifier = "device";
        public readonly string DeviceIdentifier;

        public DeviceViewer Viewer;

        public Chain Parent;
        public int? ParentIndex;
        public Action<Signal> MIDIExit = null;

        public abstract Device Clone();
        
        protected Device(string device) => DeviceIdentifier = device;

        public abstract void MIDIEnter(Signal n);

        public virtual void Dispose() => MIDIExit = null;

        public void Move(Device device) {
            this.Parent.Remove(this.ParentIndex.Value);
            if (this == device) return;

            this.Parent.Viewer.Contents_Remove(this.ParentIndex.Value);

            device.Parent.Viewer.Contents_Insert(device.ParentIndex.Value + 1, this);
            device.Parent.Insert(device.ParentIndex.Value + 1, this);
        }

        public void Move(Chain chain) {
            if (chain.Count > 0 && this == chain[0]) return;

            this.Parent.Viewer.Contents_Remove(this.ParentIndex.Value);
            this.Parent.Remove(this.ParentIndex.Value);

            chain.Viewer.Contents_Insert(0, this);
            chain.Insert(0, this);
        }

        public static Device Create(Type device, Chain parent) {
            object obj = FormatterServices.GetUninitializedObject(device);
            device.GetField("Parent").SetValue(obj, parent);

            ConstructorInfo ctor = device.GetConstructors()[0];
            ctor.Invoke(
                obj,
                BindingFlags.OptionalParamBinding,
                null, Enumerable.Repeat(Type.Missing, ctor.GetParameters().Count()).ToArray(),
                CultureInfo.CurrentCulture
            );
            
            return (Device)obj;
        }

        public static Device Decode(string jsonString) {
            Dictionary<string, object> json = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
            if (json["object"].ToString() != Identifier) return null;

            object[] specific = new object[] {json["data"].ToString()};
            
            foreach (Type device in (from type in Assembly.GetExecutingAssembly().GetTypes() where type.Namespace.StartsWith("Apollo.Devices") select type)) {
                object parsed = device.GetMethod("DecodeSpecific").Invoke(null, specific);
                if (parsed != null) return (Device)parsed;
            }

            return null;
        }

        public abstract string EncodeSpecific();
        public string Encode() {
            StringBuilder json = new StringBuilder();

            using (JsonWriter writer = new JsonTextWriter(new StringWriter(json))) {
                writer.WriteStartObject();

                    writer.WritePropertyName("object");
                    writer.WriteValue(Identifier);

                    writer.WritePropertyName("data");
                    writer.WriteRawValue(EncodeSpecific());

                writer.WriteEndObject();
            }
            
            return json.ToString();
        }
    }
}