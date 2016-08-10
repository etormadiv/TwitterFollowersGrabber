/**
 *   A class that allow get the followers/following people of a specific
 *   Twitter user, and allows to get some information about a given list of usernames.
 *   Copyright (C) 2016  Etor Madiv (etormadiv@gmail.com)
 *
 *   This program is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   This program is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *
 *   You should have received a copy of the GNU General Public License
 *   along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace TwitterFollowersGrabberClient
{
	public class Program
	{
		public static void Main()
		{
			TwitterFollowersGrabber tfg = new TwitterFollowersGrabber();
			/* Login to a twitter account */
			tfg.Login("your_twitter_username", "your_twitter_password");
			
			/* Get following people of a user */
			while(tfg.HasNextRecords)
				tfg.GetNextFriends("ahmed");
			
			/* Reset so we can perform other requests */
			tfg.ResetWithoutClear();
			
			/* Print found results */
			foreach(TwitterUserInfo info in tfg.UsersList)
			{
				Console.WriteLine(info.ID + " " + info.Username + " " + info.FollowersCount + " " + info.FollowingCount);
			}
			
			/* Reset our object including clearing the internal list */
			tfg.Reset();
			
			/* Create a list of usernames */
			List<string> usernames = new List<string>();
			usernames.Add("b7");
			usernames.Add("nasser");
			usernames.Add("ahmed");
			
			/* Retrieve some info about those people specified on the list */
			tfg.GetUsersInfo(usernames);
			
			/* Print result of those people */
			foreach(TwitterUserInfo info in tfg.UsersList)
			{
				Console.WriteLine(info.ID + " " + info.Username + " " + info.FollowersCount + " " + info.FollowingCount);
			}
		}
	}
	
	public class TwitterFollowersGrabber
	{
		/// <summary>
		/// The url that is used to get the Authenticity token.
		/// </summary>
		private const string loginUrl   = "https://twitter.com/login/";
		
		/// <summary>
		/// The url that is used to login.
		/// </summary>
		private const string sessionUrl = "https://twitter.com/sessions";
		
		/// <summary>
		/// The url of mobile version.
		/// </summary>
		private const string mobileUrl  = "https://mobile.twitter.com/";
		
		/// <summary>
		/// The url of Twitter API.
		/// </summary>
		private const string apiUrl     = "https://api.twitter.com/1.1/";
		
		/// <summary>
		/// The Authorization token used to perform the requests.
		/// </summary>
		private const string bearerAuthorizationToken = "Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA";
		
		/// <summary>
		/// The user agent used to perform the requests.
		/// </summary>
		private const string userAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/51.0.2704.103 Safari/537.36";
		
		/// <summary>
		/// Store The Authenticity token.
		/// </summary>
		private string authenticityToken;
		
		/// <summary>
		/// Store The CSRF token.
		/// </summary>
		private string csrfToken;
		
		/// <summary>
		/// Store The value of the next cursor.
		/// </summary>
		private string nextCursor;
		
		/// <summary>
		/// Store The cookies.
		/// </summary>
		private CookieContainer cookieContainer;
		
		/// <summary>
		/// Check if there is more records to retrieve.
		/// </summary>
		public  bool   HasNextRecords {get; private set;}
		
		/// <summary>
		/// Hold data of users that is extracted from twitter website.
		/// </summary>
		public List<TwitterUserInfo> UsersList = new List<TwitterUserInfo>();
		
		/// <summary>
		/// The default constructor.
		/// </summary>
		public TwitterFollowersGrabber()
		{
			Initialize();
		}
		
		/// <summary>
		/// Initialize our object to the required values.
		/// It is not intended to be called from your code directly.
		/// </summary>
		private void Initialize()
		{
			HttpWebRequest hwr = (HttpWebRequest) WebRequest.Create(loginUrl);
			hwr.UserAgent      = userAgent;
			
			cookieContainer     = new CookieContainer();
			hwr.CookieContainer = cookieContainer;
			
			string response = "";
			
			using( var hwResponse = hwr.GetResponse() )
			{
				using( var stream = hwResponse.GetResponseStream() )
				{
					using( var reader = new StreamReader(stream) )
					{
						response = reader.ReadToEnd();
					}
				}
			}
			
			int tokenStart  = response.IndexOf("authenticity_token") + 18 + 9;
			int tokenLength = response.IndexOf("\"", tokenStart) - tokenStart;
			
			authenticityToken = response.Substring(tokenStart, tokenLength);
		}
		
		/// <summary>
		/// Login to twitter website.
		/// </summary>
		/// <param name="username"> The username of the account </param>
		/// <param name="password"> The password of the account </param>
		public void Login(string username, string password)
		{
			HttpWebRequest hwr = (HttpWebRequest) WebRequest.Create(sessionUrl);
			hwr.Method      = "POST";
			hwr.UserAgent   = userAgent;
			hwr.ContentType = "application/x-www-form-urlencoded";
			hwr.Referer     = loginUrl;
			
			hwr.CookieContainer = cookieContainer;
			
			byte[] data = Encoding.ASCII.GetBytes(
				  "session[username_or_email]="  + username
				+ "&session[password]="          + password
				+ "&authenticity_token="         + authenticityToken
			);
			
			using(var stream = hwr.GetRequestStream())
			{
				stream.Write(data, 0, data.Length);
			}
			
			using(var hwResponse = (HttpWebResponse) hwr.GetResponse() )
			{
				using(var stream = hwResponse.GetResponseStream() )
				{
					using( var reader = new StreamReader(stream)  )
					{
						if(!reader.ReadToEnd().Contains("id=\"signout-form\""))
						{
							throw new TwitterException("Username and/or password incorrect !");
						}
					}
				}
			}
			
			hwr = (HttpWebRequest) WebRequest.Create(mobileUrl);
			hwr.Method      = "GET";
			hwr.UserAgent   = userAgent;
			
			hwr.CookieContainer = cookieContainer;
			
			using(var hwResponse = (HttpWebResponse) hwr.GetResponse() )
			{
				
			}
			
			CookieCollection cookies = cookieContainer.GetCookies( new Uri(mobileUrl) );
			csrfToken = cookies["ct0"].Value;
			
			Reset();
		}
		
		/// <summary>
		/// Reset our object to the initial state, including clearing the list of users.
		/// </summary>
		public void Reset()
		{
			UsersList.Clear();
			ResetWithoutClear();
		}
		
		/// <summary>
		/// Reset our object to the initial state, without clearing the list of users.
		/// </summary>
		public void ResetWithoutClear()
		{
			nextCursor     = "-1";
			HasNextRecords = true;
		}
		
		/// <summary>
		/// Get the next list of people that the specified user is currently following (a.k.a. his friends).
		/// </summary>
		/// <param name="username"> The username of a user to retrieve his following people (a.k.a. friends). </param>
		public void GetNextFriends(string username)
		{
			/* Max count = 200 */
			HttpWebRequest hwr = (HttpWebRequest) WebRequest.Create(apiUrl + "friends/list.json?cursor=" + nextCursor + "&skip_status=1&include_user_entities=false&count=200&screen_name=" + username);
			hwr.Method      = "GET";
			hwr.UserAgent   = userAgent;
			hwr.Referer     = mobileUrl;
			
			hwr.CookieContainer = cookieContainer;
			
			hwr.Headers.Add("Authorization"      , bearerAuthorizationToken);
			hwr.Headers.Add("X-Csrf-Token"       , csrfToken);
			hwr.Headers.Add("X-Twitter-Auth-Type", "OAuth2Session");
			
			string response = "";
			
			using(var hwResponse = (HttpWebResponse) hwr.GetResponse() )
			{
				using(var stream = hwResponse.GetResponseStream() )
				{
					using( var reader = new StreamReader(stream)  )
					{
						response = reader.ReadToEnd();
					}
				}
			}
			
			JavaScriptSerializer deserializer = new JavaScriptSerializer();
			TwitterResponse twitterResponse = deserializer.Deserialize<TwitterResponse>(response);
			
			foreach(User user in twitterResponse.users)
			{
				TwitterUserInfo userInfo = new TwitterUserInfo();
				
				userInfo.ID             = user.id_str;
				userInfo.Username       = user.screen_name;
				userInfo.FollowersCount = user.followers_count.ToString();
				userInfo.FollowingCount = user.friends_count.ToString();
				
				UsersList.Add(userInfo);
			}
			
			nextCursor = twitterResponse.next_cursor_str;
			HasNextRecords = (nextCursor != "0");
		}
		
		/// <summary>
		/// Get the next list of followers of the specified user.
		/// </summary>
		/// <param name="username"> The username of a user to retrieve his followers people. </param>
		public void GetNextFollowers(string username)
		{
			/* Max count = 200 */
			HttpWebRequest hwr = (HttpWebRequest) WebRequest.Create(apiUrl + "followers/list.json?cursor=" + nextCursor + "&skip_status=1&include_user_entities=false&count=200&screen_name=" + username);
			hwr.Method      = "GET";
			hwr.UserAgent   = userAgent;
			hwr.Referer     = mobileUrl;
			
			hwr.CookieContainer = cookieContainer;
			
			hwr.Headers.Add("Authorization"      , bearerAuthorizationToken);
			hwr.Headers.Add("X-Csrf-Token"       , csrfToken);
			hwr.Headers.Add("X-Twitter-Auth-Type", "OAuth2Session");
			
			string response = "";
			
			using(var hwResponse = (HttpWebResponse) hwr.GetResponse() )
			{
				using(var stream = hwResponse.GetResponseStream() )
				{
					using( var reader = new StreamReader(stream)  )
					{
						response = reader.ReadToEnd();
					}
				}
			}
			
			JavaScriptSerializer deserializer = new JavaScriptSerializer();
			TwitterResponse twitterResponse = deserializer.Deserialize<TwitterResponse>(response);
			
			foreach(User user in twitterResponse.users)
			{
				TwitterUserInfo userInfo = new TwitterUserInfo();
				
				userInfo.ID             = user.id_str;
				userInfo.Username       = user.screen_name;
				userInfo.FollowersCount = user.followers_count.ToString();
				userInfo.FollowingCount = user.friends_count.ToString();
				
				UsersList.Add(userInfo);
			}
			
			nextCursor = twitterResponse.next_cursor_str;
			HasNextRecords = (nextCursor != "0");
			
		}
		
		/// <summary>
		/// Get a string that is representing the given list of usernames, separated by comma.
		/// </summary>
		/// <param name="usernameList"> A list of usernames to be separated by comma. </param>
		/// <returns> A string containing usernames separated by comma. </returns>
		private string UsernameListToString(List<string> usernameList)
		{
			return string.Join(",", usernameList.Select(s => s.Trim()));
		}
		
		/// <summary>
		/// Get some information about the specified usernames in the given list.
		/// </summary>
		/// <param name="usernameList"> A list of usernames that we will retrieve some information about it. 
		/// The list must not contain more than 100 username. </param>
		public void GetUsersInfo(List<string> usernameList)
		{
			HttpWebRequest hwr = (HttpWebRequest) WebRequest.Create(apiUrl + "users/lookup.json?include_entities=false&screen_name=" + UsernameListToString(usernameList) );
			hwr.Method      = "GET";
			hwr.UserAgent   = userAgent;
			hwr.Referer     = mobileUrl;
			
			hwr.CookieContainer = cookieContainer;
			
			hwr.Headers.Add("Authorization"      , bearerAuthorizationToken);
			hwr.Headers.Add("X-Csrf-Token"       , csrfToken);
			hwr.Headers.Add("X-Twitter-Auth-Type", "OAuth2Session");
			
			string response = "";
			
			using(var hwResponse = (HttpWebResponse) hwr.GetResponse() )
			{
				using(var stream = hwResponse.GetResponseStream() )
				{
					using( var reader = new StreamReader(stream)  )
					{
						response = reader.ReadToEnd();
					}
				}
			}
			
			JavaScriptSerializer deserializer = new JavaScriptSerializer();
			List<User> users = deserializer.Deserialize<List<User>>(response);
			
			foreach(User user in users)
			{
				TwitterUserInfo userInfo = new TwitterUserInfo();
				
				userInfo.ID             = user.id_str;
				userInfo.Username       = user.screen_name;
				userInfo.FollowersCount = user.followers_count.ToString();
				userInfo.FollowingCount = user.friends_count.ToString();
				
				UsersList.Add(userInfo);
			}
		}
	}

	public class User
	{
		public string id_str { get; set; }
		public string screen_name { get; set; }
		public int followers_count { get; set; }
		public int friends_count { get; set; }
	}

	public class TwitterResponse
	{
		public List<User> users { get; set; }
		public string next_cursor_str { get; set; }
		public string previous_cursor_str { get; set; }
	}

	public class TwitterUserInfo
	{
		public string ID;
		public string Username;
		public string FollowersCount;
		public string FollowingCount;
	}
	
	public class TwitterException : Exception
	{
		public TwitterException(string message)
			:base(message)
		{
			
		}
	}
	
}