﻿using UnityEngine;
using System;
using System.Collections.Generic;
using WebSocketSharp;
using WebSocketSharp.Server;
using WebSocketSharp.Net;
using System.Threading;
using MsgPack;

namespace MLPlayer
{
	public class AIServer : MonoBehaviour
	{
		private WebSocketServer wssv;

		[SerializeField] string domain;
		[SerializeField] int port;
		public Queue<byte[]> agentMessageQueue;
		private Queue<byte[]> aiMessageQueue;
		private Mutex mutAgent;
        public Dictionary<int, Agent> agents = new Dictionary<int, Agent>();
		private MsgPack.CompiledPacker packer;

        public AIServer (Dictionary<int, Agent> _agents)
        {
            agents = _agents;
            mutAgent = new Mutex ();
            packer = new MsgPack.CompiledPacker ();
            agentMessageQueue = new Queue<byte[]> ();
            aiMessageQueue = new Queue<byte[]> ();
        }

        public void Init (Dictionary<int, Agent> _agents)
        {
            agents = _agents;
            mutAgent = new Mutex ();
            packer = new MsgPack.CompiledPacker ();
            agentMessageQueue = new Queue<byte[]> ();
            aiMessageQueue = new Queue<byte[]> ();
        }

		public class CommunicationGym : WebSocketBehavior
		{
            public Dictionary<int, Agent> agents { set; get; }
			MsgPack.BoxingPacker packer = new MsgPack.BoxingPacker ();
			private bool SendFlag=true;

			protected override void OnMessage (MessageEventArgs e)
			{
				//receive message 
                System.Object[] actionList = (System.Object[])packer.Unpack(e.RawData);

                for (int idx = 0; idx < actionList.Length; idx++)
                {
                    Dictionary<System.Object,System.Object> action = (Dictionary<System.Object,System.Object>)actionList[idx];
                    var originalKey = new Dictionary<string, byte[]>();
                    foreach (byte[] key in action.Keys) {
                        Debug.Log (System.Text.Encoding.UTF8.GetString(key));
                        originalKey.Add(System.Text.Encoding.UTF8.GetString(key), key);
                    }
                    agents[(int)action[originalKey["agent_id"]]].action.Set(action);
                    Agent agent = agents[(int)action[originalKey["agent_id"]]];
                    Debug.Log ("Rotate=" + agent.action.rotate + " Forword=" + agent.action.forward + " Jump=" + agent.action.fire);
                }

				SceneController.received.Set ();

				//send state data 
				Sendmessage();
			
			}

			protected override void OnOpen ()
			{
				Debug.Log ("Socket Open");
				SceneController.received.Set ();
				Sendmessage ();
			}

			protected override void OnClose(CloseEventArgs e)
			{
				SceneController.FinishFlag=true;
				SceneController.received.Set ();
			}
				
			private void Sendmessage(){
				SendFlag = true;
				//send state data 
				while (SendFlag == true) {
                    if (SceneController.Instance.server.agentMessageQueue.Count > 0) {
                        byte[] data = SceneController.Instance.server.PopAgentState ();
						Send (data);
						SendFlag = false;
					}
				}
			}
		}

		CommunicationGym instantiate ()
		{
			CommunicationGym service = new CommunicationGym ();
            service.agents = agents;
            
			return service;
		}

		string GetUrl(string domain,int port){
			return "ws://" + domain + ":" + port.ToString ();
		}

		public void AIServerStart ()
		{
			Debug.Log (GetUrl(domain,port));
			wssv = new WebSocketServer (GetUrl(domain,port));
			wssv.AddWebSocketService<CommunicationGym> ("/CommunicationGym", instantiate);
			wssv.Start ();


			if (wssv.IsListening) {
				Debug.Log ("Listening on port " + wssv.Port + ", and providing WebSocket services:");
				foreach (var path in wssv.WebSocketServices.Paths)
					Debug.Log ("- " + path);
			}
		}

		public void PushAIMessage (byte[] msg)
		{
			throw new System.NotImplementedException ();
		}

		public byte[] PopAIMessage ()
		{
			throw new System.NotImplementedException ();
		}

		public void PushAgentState (State s)
		{
			byte[] msg = packer.Pack (s);  
			mutAgent.WaitOne ();
			agentMessageQueue.Enqueue (msg);  
			mutAgent.ReleaseMutex ();
		}

        public void PushAgentState (State[] s)
        {
            byte[] msg = packer.Pack (s);  
            mutAgent.WaitOne ();
            agentMessageQueue.Enqueue (msg);  
            mutAgent.ReleaseMutex ();
        }

		public byte[] PopAgentState ()
		{
			byte[] received = null;

			mutAgent.WaitOne ();
			if (agentMessageQueue.Count > 0) {
				received = agentMessageQueue.Dequeue ();
			}
			mutAgent.ReleaseMutex ();

			return received;
		}

		void OnApplicationQuit ()
		{
			wssv.Stop ();
			Debug.Log ("websocket server exiteed");
		}
	}
}
