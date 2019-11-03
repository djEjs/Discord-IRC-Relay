/*  Discord IRC Relay - A Discord & IRC bot that relays messages 
 *
 *  Copyright (C) 2018 Michael Flaherty // michaelwflaherty.com // michaelwflaherty@me.com
 * 
 * This program is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by the Free
 * Software Foundation, either version 3 of the License, or (at your option) 
 * any later version.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT 
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS 
 * FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along with 
 * this program. If not, see http://www.gnu.org/licenses/.
 */

using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;

using IRCRelay.Logs;

namespace IRCRelay.Emoji
{
    public class EmojiManager
    {
        private Dictionary<string, string> emojiMap = new Dictionary<string, string>();
        private dynamic config;

        private EmojiManager()
        {
        }
        public static EmojiManager Instance { get { return Nested.instance; } }

        private class Nested
        {
            internal static readonly EmojiManager instance = new EmojiManager();
            static Nested()
            {
            }
        }

        public void setConfig(dynamic config)
        {
            this.config = config;
        }


        public void SaveEmoji(String emojiString, String simpleString)
        {
            if (config.IRCLogMessages)
                LogManager.WriteLog("[SaveEmoji] " + simpleString + " -> " + emojiString, "log.txt");
            emojiMap.Add(simpleString, emojiString);
        }

        public String ReplaceEmoji(String simpleString)
        {
            if(emojiMap.ContainsKey(simpleString))
            {
                return emojiMap[simpleString];
            }
            else
            {
                return simpleString;
            }
        }
    }
}