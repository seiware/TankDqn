﻿using UnityEngine;
using System.Collections;

namespace MLPlayer {
	public class State {
		public float reward;
        public int step_id;
        public int agent_id;
		public bool endEpisode;
		public byte[][] image;
		public byte[][] depth;
		public void Clear() {
			reward = 0;
			endEpisode = false;
			image = null;
			depth = null;
		}
	}
}