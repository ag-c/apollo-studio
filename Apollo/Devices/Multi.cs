using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using Apollo.Core;
using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Structures;

namespace Apollo.Devices {
    public class Multi: Device, IMultipleChainParent, ISelectParent {
        public static readonly new string DeviceIdentifier = "multi";

        public IMultipleChainParentViewer SpecificViewer {
            get => (IMultipleChainParentViewer)Viewer.SpecificViewer;
        }

        public ISelectParentViewer IViewer {
            get => (ISelectParentViewer)Viewer.SpecificViewer;
        }

        public List<ISelect> IChildren {
            get => Chains.Select(i => (ISelect)i).ToList();
        }

        public bool IRoot {
            get => false;
        }

        private Action<Signal> _midiexit;
        public override Action<Signal> MIDIExit {
            get => _midiexit;
            set {
                _midiexit = value;
                Reroute();
            }
        }

        public Chain Preprocess;
        public List<Chain> Chains = new List<Chain>();

        private Random RNG = new Random();
        
        public enum MultiType {
            Forward,
            Backward,
            Random,
            RandomPlus
        }

        private MultiType _mode;
        public string Mode {
            get {
                if (_mode == MultiType.Forward) return "Forward";
                else if (_mode == MultiType.Backward) return "Backward";
                else if (_mode == MultiType.Random) return "Random";
                else if (_mode == MultiType.RandomPlus) return "Random+";
                return null;
            }
            set {
                if (value == "Forward") _mode = MultiType.Forward;
                else if (value == "Backward") _mode = MultiType.Backward;
                else if (value == "Random") _mode = MultiType.Random;
                else if (value == "Random+") _mode = MultiType.RandomPlus;

                if (SpecificViewer != null) ((MultiViewer)SpecificViewer).SetMode(Mode);
            }
        }

        public MultiType GetMultiMode() => _mode;

        private int current = -1;
        private ConcurrentDictionary<Signal, int> buffer = new ConcurrentDictionary<Signal, int>();

        private void Reroute() {
            Preprocess.Parent = this;
            Preprocess.MIDIExit = PreprocessExit;

            for (int i = 0; i < Chains.Count; i++) {
                Chains[i].Parent = this;
                Chains[i].ParentIndex = i;
                Chains[i].MIDIExit = ChainExit;
            }
        }

        public Chain this[int index] {
            get => Chains[index];
        }

        public int Count {
            get => Chains.Count;
        }

        public override Device Clone() => new Multi(Preprocess.Clone(), (from i in Chains select i.Clone()).ToList(), Expanded, _mode) {
            Collapsed = Collapsed,
            Enabled = Enabled
        };

        public void Insert(int index, Chain chain = null) {
            Chains.Insert(index, chain?? new Chain());
            Reroute();

            SpecificViewer?.Contents_Insert(index, Chains[index]);
            
            Track.Get(this)?.Window?.Selection.Select(Chains[index]);
            SpecificViewer?.Expand(index);
        }

        public void Remove(int index, bool dispose = true) {
            SpecificViewer?.Contents_Remove(index);

            if (dispose) Chains[index].Dispose();
            Chains.RemoveAt(index);
            Reroute();
            
            if (index < Chains.Count)
                Track.Get(this)?.Window?.Selection.Select(Chains[index]);
            else if (Chains.Count > 0)
                Track.Get(this)?.Window?.Selection.Select(Chains.Last());
            else
                Track.Get(this)?.Window?.Selection.Select(null);
        }

        private void Reset() => current = -1;

        public int? Expanded { get; set; }

        public Multi(Chain preprocess = null, List<Chain> init = null, int? expanded = null, MultiType mode = MultiType.Forward): base(DeviceIdentifier) {
            Preprocess = preprocess?? new Chain();

            foreach (Chain chain in init?? new List<Chain>()) Chains.Add(chain);
            
            Expanded = expanded;

            _mode = mode;
            
            Launchpad.MultiReset += Reset;

            Reroute();
        }

        private void ChainExit(Signal n) => MIDIExit?.Invoke(n);

        public override void MIDIProcess(Signal n) {
            Signal m = n.Clone();
            n.Color = new Color();

            if (!buffer.ContainsKey(n)) {
                if (!m.Color.Lit) return;

                if (_mode == MultiType.Forward) {
                    if (++current >= Chains.Count) current = 0;
                
                } else if (_mode == MultiType.Backward) {
                    if (--current < 0) current = Chains.Count - 1;
                
                } else if (_mode == MultiType.Random || current == -1)
                    current = RNG.Next(Chains.Count);
                
                else if (_mode == MultiType.RandomPlus) {
                    int old = current;
                    current = RNG.Next(Chains.Count - 1);
                    if (current >= old) current++;
                }

                m.MultiTarget = buffer[n] = current;

            } else {
                m.MultiTarget = buffer[n];
                if (!m.Color.Lit) buffer.Remove(n, out int _);
            }

            Preprocess.MIDIEnter(m);
        }

        private void PreprocessExit(Signal n) {
            int target = n.MultiTarget.Value;
            n.MultiTarget = null;
            
            if (Chains.Count == 0) {
                MIDIExit?.Invoke(n);
                return;
            }
            
            Chains[target].MIDIEnter(n);
        }

        public override void Dispose() {
            Preprocess.Dispose();
            foreach (Chain chain in Chains) chain.Dispose();
            base.Dispose();
        }
    }
}