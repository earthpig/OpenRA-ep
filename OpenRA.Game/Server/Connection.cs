#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace OpenRA.Server
{
	public class Connection
	{
		public Socket socket;
		public List<byte> data = new List<byte>();
		public ReceiveState State = ReceiveState.Header;
		public int ExpectLength = 8;
		public int Frame = 0;

		public int MostRecentFrame = 0;

		/* client data */
		public int PlayerIndex;

		public byte[] PopBytes(int n)
		{
			var result = data.GetRange(0, n);
			data.RemoveRange(0, n);
			return result.ToArray();
		}

		bool ReadDataInner( Server server )
		{
			var rx = new byte[1024];
			var len = 0;

			for (; ; )
			{
				try
				{
					if (0 < (len = socket.Receive(rx)))
						data.AddRange(rx.Take(len));
					else
					{
						if (len == 0)
							server.DropClient(this);
						break;
					}

				}
				catch (SocketException e)
				{
					if (e.SocketErrorCode == SocketError.WouldBlock) break;
					server.DropClient(this);
					return false;
				}
			}

			return true;
		}

		public void ReadData( Server server )
		{
			if (ReadDataInner(server))
				while (data.Count >= ExpectLength)
				{
					var bytes = PopBytes(ExpectLength);
					switch (State)
					{
						case ReceiveState.Header:
							{
								ExpectLength = BitConverter.ToInt32(bytes, 0) - 4;
								Frame = BitConverter.ToInt32(bytes, 4);
								State = ReceiveState.Data;
							} break;

						case ReceiveState.Data:
							{
								server.DispatchOrders(this, Frame, bytes);
								MostRecentFrame = Frame;
								ExpectLength = 8;
								State = ReceiveState.Header;

								server.UpdateInFlightFrames(this);
							} break;
					}
				}
		}}

	public enum ReceiveState { Header, Data };
}
