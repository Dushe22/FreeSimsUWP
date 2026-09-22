#region Copyright � 2003 Dave Lewis [logthis@tpsd.com]
/*
 * 6/15/2003
 * This software is provided 'as-is', without any express or implied warranty.
 * In no event will the author(s) be held liable for any damages arising from
 * the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 *   1. The origin of this software must not be misrepresented; you must not
 *      claim that you wrote the original software. If you use this software
 *      in a product, an acknowledgment in the product documentation would be
 *      appreciated but is not required.
 * 
 *   2. Altered source versions must be plainly marked as such, and must not
 *      be misrepresented as being the original software.
 * 
 *   3. This notice may not be removed or altered from any source distribution.
 */ 
#endregion
// Platform extraction for the UWP port; desktop behavior retained.
using System;
using System.Diagnostics;
namespace LogThis
{
    internal static class PlatformLog
    {
        public static string[] DefaultFile()
        {
            return Log.GetBasename(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        public static string FallbackDirectory { get { return Environment.CurrentDirectory; } }
		public static void WriteEvent(string source, string sText, eloglevel loglevel)
		{
			EventLogEntryType EventType;
			switch(loglevel)
			{
				case eloglevel.error:
					EventType = EventLogEntryType.Error;
					break;
				case eloglevel.warn:
					EventType = EventLogEntryType.Warning;
					break;
				case eloglevel.info:
					EventType = EventLogEntryType.Information;
					break;
				default:
					EventType = EventLogEntryType.Information;
					break;
			}
			//open and write to event log.
			System.Diagnostics.EventLog oEV = new System.Diagnostics.EventLog();
			oEV.Source = source;
			oEV.WriteEntry (sText, EventType);
			oEV.Close();
		}

    }
}
