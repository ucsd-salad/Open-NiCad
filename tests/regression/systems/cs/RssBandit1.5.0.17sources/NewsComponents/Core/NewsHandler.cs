#region CVS Version Header
/*
 * $Id: NewsHandler.cs,v 1.188 2007/09/17 22:09:51 carnage4life Exp $
 * Last modified by $Author: carnage4life $
 * Last modified at $Date: 2007/09/17 22:09:51 $
 * $Revision: 1.188 $
 */
#endregion

#region framework usings
using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml; 
using System.Xml.Schema;
using System.IO; 
using System.Collections;
using System.Collections.Specialized;
using System.Xml.Serialization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml.Xsl;
using System.Net;
using System.Diagnostics;
using System.Text;
#endregion

#region project usings
using NewsComponents.Collections;
using NewsComponents.Feed;
using NewsComponents.News;
using NewsComponents.Net;
using NewsComponents.Resources;
using NewsComponents.Search;
using NewsComponents.RelationCosmos;
using NewsComponents.Storage;
using NewsComponents.Utils;
#endregion


namespace NewsComponents {

	/// <summary>
	/// Supported Feedlist Formats (import/export).
	/// </summary>
	public enum FeedListFormat{
		/// <summary>
		/// Open Content Syndication. See http://internetalchemy.org/ocs/
		/// </summary>
		OCS,
		/// <summary>
		/// Outline Processor Markup Language, see http://opml.scripting.com/spec
		/// </summary>
		OPML, 
		/// <summary>
		/// Native NewsHandler format
		/// </summary>
		NewsHandler,
		/// <summary>
		/// Native reduced/light NewsHandler format
		/// </summary>
		NewsHandlerLite,
	}


	/// <summary>
	/// Class for managing News feeds. This class is NOT thread-safe.
	/// </summary>
	public class NewsHandler {

		#region ctor's
		/// <summary>
		/// Initialize the userAgent template
		/// </summary>
		static NewsHandler()	{	
			
			StringBuilder sb = new StringBuilder(200);
			sb.Append("{0}");	// userAgent filled in later
			sb.Append(" (.NET CLR ");
			sb.Append(Environment.Version);
			sb.Append("; ");
			sb.Append(Environment.OSVersion.ToString().Replace("Microsoft Windows ", "Win"));
			sb.Append("; http://www.rssbandit.org");
			sb.Append(")");

			userAgentTemplate = sb.ToString();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NewsHandler"/> class
		/// with a default configuration.
		/// </summary>
		public NewsHandler(): 
			this(NewsComponentsConfiguration.Default) {
		}
		
		/// <summary>
		/// Initializes a new instance of the <see cref="NewsHandler"/> class.
		/// </summary>
		/// <param name="configuration">The configuration.</param>
		public NewsHandler(INewsComponentsConfiguration configuration) 
		{
			this.configuration = configuration;
			if (this.configuration == null)
				this.configuration = new NewsComponentsConfiguration();
			
			// check for programmers error in configuration:
			ValidateAndThrow(this.configuration);
			
			this.LoadFeedlistSchema();   
						
			this.rssParser = new RssParser(this);
			this.searchHandler = new LuceneSearch(this.configuration, this);			
			
			// initialize (later on loaded from feedlist):
			this.PodcastFolder = this.configuration.DownloadedFilesDataPath;
			this.EnclosureFolder = this.configuration.DownloadedFilesDataPath;
			
			if (this.EnclosureFolder != null) {
				this.enclosureDownloader = new BackgroundDownloadManager(this.configuration, this); 
				this.enclosureDownloader.DownloadCompleted +=  new DownloadCompletedEventHandler(this.OnEnclosureDownloadComplete);
			}

			this.AsyncWebRequest = new AsyncWebRequest();
			this.AsyncWebRequest.OnAllRequestsComplete += new AsyncWebRequest.RequestAllCompleteCallback(this.OnAllRequestsComplete);

		}
		
//		/// <summary>
//		/// Initializes class. 
//		/// </summary>
//		public NewsHandler(): this(null, null,null){;}
//
//
//		/// <summary>		
//		/// </summary>
//		/// <param name="cm">The object that manages the on-disk cache of feeds for the 
//		/// application. </param>
//		public NewsHandler(CacheManager cm): this(null, cm, null) {;}
//
//		/// <summary>
//		/// Constructor initializes class.
//		/// </summary>
//		/// <param name="applicationName">The Application Name or ID that uses the component. This will be used to 
//		/// initialize the user path to store the feeds file and cached items.</param>
//		public NewsHandler(string applicationName): this(applicationName, null,null) {;}
//
//
//		/// <summary>
//		/// Constructor initializes class.
//		/// </summary>
//		/// <param name="applicationName">The Application Name or ID that uses the component. This will be used to
//		/// initialize the user path to store the feeds file and cached items.</param>
//		/// <param name="cm">The object that manages the on-disk cache of feeds for the
//		/// application.</param>
//		/// <param name="searchIndexPath">The search index path.</param>
//		public NewsHandler(string applicationName, CacheManager cm, string searchIndexPath){
//      
//			this.LoadFeedlistSchema();   
//						
//			if(!StringHelper.EmptyOrNull(applicationName)){
//				this.applicationName = applicationName;
//			}
//
//			this.cacheHandler    = cm; 
//
//			if(this.cacheHandler == null){
//				string path = Path.Combine(GetUserPath(this.applicationName), "Cache");
//				if (!File.Exists(path))
//					Directory.CreateDirectory(path);
//				this.cacheHandler = new FileCacheManager(path);  
//			}
//
//			this.rssParser = new RssParser(this);
//			this.searchHandler = new LuceneSearch(this, searchIndexPath);			
//			
//			this.enclosureDownloader = new BackgroundDownloadManager(this.applicationName, this); 
//			this.enclosureDownloader.DownloadCompleted +=  new DownloadCompletedEventHandler(this.OnEnclosureDownloadComplete);
//
//			this.AsyncWebRequest = new AsyncWebRequest();
//			this.AsyncWebRequest.OnAllRequestsComplete += new AsyncWebRequest.RequestAllCompleteCallback(this.OnAllRequestsComplete);
//
//		}

		#endregion


		/// <summary>
		/// Configuration provider
		/// </summary>
		private INewsComponentsConfiguration configuration = null;
		/// <summary>
		/// Gets the NewsComponents configuration.
		/// </summary>
		/// <value>The configuration.</value>
		public INewsComponentsConfiguration Configuration {
			get { return this.configuration; }
		}

		/// <summary>
		/// Validates the configuration and throw on errors (required settings).
		/// </summary>
		/// <param name="configuration">The configuration.</param>
		private static void ValidateAndThrow(INewsComponentsConfiguration configuration) {
			if (configuration == null)
				throw new ArgumentNullException("configuration");
			if (StringHelper.EmptyOrNull(configuration.ApplicationID))
				throw new InvalidOperationException("INewsComponentsConfiguration.ApplicationID cannot be null or empty.");
			if (configuration.CacheManager == null)
				throw new InvalidOperationException("INewsComponentsConfiguration.CacheManager cannot be null.");
			if (configuration.PersistedSettings == null)
				throw new InvalidOperationException("INewsComponentsConfiguration.PersistedSettings cannot be null.");
			if (StringHelper.EmptyOrNull(configuration.UserApplicationDataPath))
				throw new InvalidOperationException("INewsComponentsConfiguration.UserApplicationDataPath cannot be null or empty.");
			if (StringHelper.EmptyOrNull(configuration.UserLocalApplicationDataPath))
				throw new InvalidOperationException("INewsComponentsConfiguration.UserLocalApplicationDataPath cannot be null or empty.");
		}
		
		/// <summary>
		/// Gets the cache manager.
		/// </summary>
		/// <value>The cache manager.</value>
		internal CacheManager CacheHandler {
			get { return configuration.CacheManager; }
		}
		
		/// <summary>
		/// Used for making asynchronous Web requests
		/// </summary>
		private AsyncWebRequest AsyncWebRequest = null; 

		/// <summary>
		/// Downloads enclosures/podcasts in the background using BITS. 
		/// </summary>
		private BackgroundDownloadManager enclosureDownloader; 

//		/// <summary>
//		/// Manages the cache. 
//		/// </summary>
//		[Obsolete("Use CacheHandler property")]
//		private CacheManager cacheHandler; 

		/// <summary>
		/// The location where feed items are cached.
		/// </summary>
		internal string CacheLocation{
			get{
				return this.CacheHandler.CacheLocation;
			}
		}

		/// <summary>
		/// Manages the FeedType.Rss 
		/// </summary>
		private RssParser rssParser;

		/// <summary>
		/// Provide access to the RssParser for Rss specific tasks
		/// </summary>
		internal RssParser RssParser {
			get { return this.rssParser; }
		}

		/// <summary>
		/// Manage the lucene search 
		/// </summary>
		private LuceneSearch searchHandler;

		/// <summary>
		/// Gets or sets the search index handler.
		/// </summary>
		/// <value>The search handler.</value>
		public LuceneSearch SearchHandler {
			get { return this.searchHandler; }
			set { this.searchHandler = value; }
		}

		/// <summary>
		/// Gets a empty item list.
		/// </summary>
		public static readonly ArrayList EmptyItemList = new ArrayList(0);

		// logging/tracing:
		private static readonly log4net.ILog _log = RssBandit.Common.Logging.Log.GetLogger(typeof(NewsHandler));

		/// <summary>
		/// Manage the NewsItem relations
		/// </summary>
		private static RelationCosmos.IRelationCosmos relationCosmos = RelationCosmosFactory.Create();

		/// <summary>
		/// Manage the channel processors working on received items and feeds
		/// </summary>
		private static NewsChannelServices receivingNewsChannel = new NewsChannelServices();

		/// <summary>
		/// Proxy server information used for connections when fetching feeds. 
		/// </summary>
		private IWebProxy proxy = GlobalProxySelection.GetEmptyWebProxy(); 

		/// <summary>
		/// Proxy server information used for connections when fetching feeds. 
		/// </summary>
		public IWebProxy Proxy{
			set{ 
				proxy = value; 
				RssParser.GlobalProxy = value;
			}
			get { return proxy;}		
		}
				

		/// <summary>
		/// Indicates whether the cookies from IE should be taken over for our own requests. 
		/// Default is true.
		/// </summary>
		private static bool setCookies = true; 

		/// <summary>
		/// Indicates whether the cookies from IE should be taken over for our own requests. 
		/// Default is true.
		/// </summary>
		public static bool SetCookies{
			set { setCookies = value; }
			get { return setCookies;  }		
		}

		/// <summary>
		/// Indicates whether the relationship cosmos should be built for incoming news items. 
		/// </summary>
		internal static bool buildRelationCosmos = true; 

		/// <summary>
		/// Indicates whether the relationship cosmos should be built for incoming news items. 
		/// </summary>
		public static bool BuildRelationCosmos{
			set {
				buildRelationCosmos = value;
				if (buildRelationCosmos == false)
					relationCosmos.Clear();
			}
			get { return buildRelationCosmos; }		
		}
		

		/// <summary>
		/// Indicates whether the application is offline or not. 
		/// </summary>
		private bool offline = false; 

		/// <summary>
		/// Indicates whether the application is offline or not. 
		/// </summary>
		public bool Offline{
			set { 
				offline = value; 
				RssParser.Offline = value;
			}
			get { return offline; }
		}

    
		/// <summary>
		/// Internal flag used after loading feed list to indicate that a category attribute of a feed is not 
		/// listed as one of the category elements. 
		/// </summary>
		private static bool categoryMismatch = false; 

		#region Trace support
		private static bool traceMode = false; 
		
		/// <summary>
		/// Boolean flag indicates whether errors should be written to a logfile 
		///	using Trace.Write(); 
		/// </summary>
		public static bool TraceMode{
			set {traceMode = value; }
			get {return traceMode; }
		}

		private static void Trace(string message) {
			Trace("{0}", message);
		}
		private static void Trace(string formatString, params object[] paramArray ) {
			if (traceMode)
				_log.Info(String.Format(formatString, paramArray));
		}
		#endregion

		private static bool unconditionalCommentRss = false;
		/// <summary>
		/// Boolean flag indicates whether the commentCount should be considered
		/// for NewsItem.HasExternalRelations() tests.
		///	 Default is false and will test both the CommentRssUrl as a non-empty string
		///	 and commentCount > 0 (zero)
		/// </summary>
		public static bool UnconditionalCommentRss{
			set {unconditionalCommentRss = value; }
			get {return unconditionalCommentRss; }
		}

		#region Feed Credentials handling

		/// <summary>
		/// Creates the credentials from a feed.
		/// </summary>
		/// <param name="f">The feed</param>
		/// <returns>ICredentials</returns>
		public static ICredentials CreateCredentialsFrom(feedsFeed f) {
		
			if (f != null && !StringHelper.EmptyOrNull(f.authUser)) {
				string u = null, p = null;
				GetFeedCredentials(f, ref u, ref p);
				return CreateCredentialsFrom(f.link, u, p);
			}
			return null;
		}

		/// <summary>
		/// Creates the credentials from an url.
		/// </summary>
		/// <param name="url">The URL.</param>
		/// <param name="domainUser">The domain user.</param>
		/// <param name="password">The password.</param>
		/// <returns>ICredentials</returns>
		public static ICredentials CreateCredentialsFrom(string url, string domainUser, string password) {
			ICredentials c = null;
			
			if (!StringHelper.EmptyOrNull(domainUser)) {
				
				NetworkCredential credentials = CreateCredentialsFrom(domainUser, password);
				try{
					Uri feedUri = new Uri(url);
					CredentialCache cc = new CredentialCache(); 
					cc.Add(feedUri, "Basic", credentials); 
					cc.Add(feedUri, "Digest", credentials); 
					cc.Add(feedUri, "NTLM", credentials); 
					c = cc;
				} catch (UriFormatException){
					c = credentials;
				}

			}
			return c;
		}
		/// <summary>
		/// Create and return a ICredentials object with the provided informations.
		/// </summary>
		/// <param name="domainUser">username and optional a domain: DOMAIN\user</param>
		/// <param name="password">the pwd</param>
		/// <returns>NetworkCredential</returns>
		public static NetworkCredential CreateCredentialsFrom(string domainUser, string password) {
			NetworkCredential c = null;
			if (domainUser != null) {
				  
				NetworkCredential credentials = null;
				string[] aDomainUser = domainUser.Split(new char[]{'\\'});
				if (aDomainUser.GetLength(0) > 1)	// Domain specified: e.g. Domain\UserName
					credentials = new NetworkCredential(aDomainUser[1], password, aDomainUser[0]);
				else
					credentials = new NetworkCredential(aDomainUser[0], password);

				c = credentials;
			}
			return c;
		}

		/// <summary>
		/// Set the authorization credentials for a feed.
		/// </summary>
		/// <param name="f">feedsFeed to be modified</param>
		/// <param name="user">username, identifier</param>
		/// <param name="pwd">password</param>
		public static void SetFeedCredentials(feedsFeed f, string user, string pwd) {
			if (f == null) return;
			f.authPassword = CryptHelper.EncryptB(pwd);
			f.authUser = user;
		}

		/// <summary>
		/// Get the authorization credentials for a feed.
		/// </summary>
		/// <param name="f">feedsFeed, where the credentials are taken from</param>
		/// <param name="user">String return parameter containing the username</param>
		/// <param name="pwd">String return parameter, containing the password</param>
		public static void GetFeedCredentials(feedsFeed f, ref string user, ref string pwd) {
			if (f == null) return;
			pwd = CryptHelper.Decrypt(f.authPassword);
			user = f.authUser;
		}


		/// <summary>
		/// Return ICredentials of a feed. 
		/// </summary>
		/// <param name="feedUrl">url of the feed</param>
		/// <returns>null in the case the feed does not have credentials</returns>
		public ICredentials GetFeedCredentials(string feedUrl) {
			if (feedUrl != null && FeedsTable.Contains(feedUrl))
				return GetFeedCredentials(FeedsTable[feedUrl]);
			return null;
		}

		/// <summary>
		/// Return ICredentials of a feed. 
		/// </summary>
		/// <param name="f">feedsFeed</param>
		/// <returns>null in the case the feed does not have credentials</returns>
		public static ICredentials GetFeedCredentials(feedsFeed f) {
			ICredentials c = null;
			if (f != null && f.authUser != null) {
				return CreateCredentialsFrom(f);
				//				string u = null, p = null;
				//				GetFeedCredentials(f, ref u, ref p);
				//				c = CreateCredentialsFrom(u, p);
			}
			return c;
		}

		#endregion

		#region NntpServerDefinition Credentials handling

		/// <summary>
		/// Set the authorization credentials for a Nntp Server.
		/// </summary>
		/// <param name="sd">NntpServerDefinition to be modified</param>
		/// <param name="user">username, identifier</param>
		/// <param name="pwd">password</param>
		public static void SetNntpServerCredentials(NntpServerDefinition sd, string user, string pwd) {
			if (sd == null) return;
			sd.AuthPassword = CryptHelper.EncryptB(pwd);
			sd.AuthUser = user;
		}

		/// <summary>
		/// Get the authorization credentials for a feed.
		/// </summary>
		/// <param name="sd">NntpServerDefinition, where the credentials are taken from</param>
		/// <param name="user">String return parameter containing the username</param>
		/// <param name="pwd">String return parameter, containing the password</param>
		public static void GetNntpServerCredentials(NntpServerDefinition sd, ref string user, ref string pwd) {
			if (sd == null) return;
			pwd = (sd.AuthPassword != null ? CryptHelper.Decrypt(sd.AuthPassword): null);
			user = sd.AuthUser;
		}


		/// <summary>
		/// Return ICredentials of a nntp server. 
		/// </summary>
		/// <param name="serverAccountName">account name of the server</param>
		/// <returns>null in the case the server does not have credentials</returns>
		public ICredentials GetNntpServerCredentials(string serverAccountName) {
			if (serverAccountName != null && nntpServers.Contains(serverAccountName))
				return GetFeedCredentials((NntpServerDefinition)nntpServers[serverAccountName]);
			return null;
		}

		/// <summary>
		/// Gets the NNTP server credentials for a feed.
		/// </summary>
		/// <param name="f">The feed.</param>
		/// <returns>ICredentials</returns>
		internal ICredentials GetNntpServerCredentials(feedsFeed f) {
			
			ICredentials c = null;
			if (f == null || ! RssHelper.IsNntpUrl(f.link))
				return c;

			try{
				Uri feedUri = new Uri(f.link);						
						
				foreach(NntpServerDefinition nsd  in this.nntpServers.Values){
					if(nsd.Server.Equals(feedUri.Authority)){
						c = this.GetNntpServerCredentials(nsd.Name);
						break;
					}
				}

			} catch (UriFormatException){;}
			return c;
		}

		/// <summary>
		/// Return ICredentials of a feed. 
		/// </summary>
		/// <param name="sd">NntpServerDefinition</param>
		/// <returns>null in the case the nntp server does not have credentials</returns>
		public static ICredentials GetFeedCredentials(NntpServerDefinition sd) {
			ICredentials c = null;
			if (sd.AuthUser != null) {
				string u = null, p = null;
				GetNntpServerCredentials(sd, ref u, ref p);
				c = CreateCredentialsFrom(u, p);
			}
			return c;
		}

		#endregion

		/// <summary>
		/// Gets the refresh rate for a particular feed
		/// </summary>
		public static void GetRefreshRate(){
			//TODO
		}

		/// <summary>
		/// Returns the user path used to store the current feed and cached items.
		/// </summary>
		/// <param name="appname">The application name that uses the component.</param>
		/// <returns></returns>
		public static string GetUserPath(string appname) {
			string s = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appname);
			if(!Directory.Exists(s)) Directory.CreateDirectory(s);
			return s;
		}
		
		/// <summary>
		/// Maximum item age. Default value is 3 months.
		/// </summary>
		private TimeSpan maxitemage = new TimeSpan(90, 0, 0, 0);
		
		/// <summary>
		/// Gets or sets the maximum amount of time an item should be kept in the 
		/// cache. This value is used for all feeds unless one is specified on 
		/// the particular feed or its category
		/// </summary>
		public  TimeSpan MaxItemAge { 
			get{ return this.maxitemage;}  
			
			[MethodImpl(MethodImplOptions.Synchronized)]
			set{ 
				this.maxitemage = value;				

				string[] keys;
			
				lock (FeedsTable.SyncRoot) {
					keys = new string[FeedsTable.Count];
					if (FeedsTable.Count > 0)
						FeedsTable.Keys.CopyTo(keys, 0);	
				}
				
				for(int i = 0, len = keys.Length; i < len; i++){
					FeedsTable[keys[i]].maxitemage = XmlConvert.ToString((TimeSpan)value); 
				}
			} 
		}

		/// <summary>
		/// The stylesheet for displaying feeds.
		/// </summary>
		private string stylesheet;
		
		/// <summary>
		/// Gets or sets the stylesheet for displaying feeds
		/// </summary>
		public  string Stylesheet { 
			get{ return this.stylesheet;}  
			
			set{ this.stylesheet = value; } 
		}

		/// <summary>
		/// The folder for downloading enclosures.
		/// </summary>
		private string enclosurefolder;
		
		/// <summary>
		/// Gets or sets the folder for downloading enclosures
		/// </summary>
		public  string EnclosureFolder { 
			get{ return this.enclosurefolder;}  
			
			set{ 
				this.enclosurefolder = value; 
			 } 
		}	


		/// <summary>
		/// The file extensions of enclosures that should be treated as podcasts. 
		/// </summary>
		private ArrayList podcastfileextensions = new ArrayList();

		/// <summary>
		/// Gets the list of file extensions of enclosures that should be treated as podcasts
		/// as a string. 
		/// </summary>
		public  string PodcastFileExtensionsAsString { 
			get{ 
				StringBuilder toReturn = new StringBuilder();
				
				foreach(string s in this.podcastfileextensions){
					if(!StringHelper.EmptyTrimOrNull(s)){
						toReturn.Append(s);
						toReturn.Append(";");
					}
				}

				return toReturn.ToString();
			}  

			set{
				string[] fileexts = value.Split(new char[]{';', ' '});
				this.podcastfileextensions.Clear(); 

				foreach (string s in fileexts){
					this.podcastfileextensions.Add(s); 
				}

			}
			 
		}

		/// <summary>
		/// The folder for downloading podcasts.
		/// </summary>
		private string podcastfolder;
		
		/// <summary>
		/// Gets or sets the folder for downloading podcasts
		/// </summary>
		public  string PodcastFolder { 
			get{ return this.podcastfolder;}  
			
			set{ 
				this.podcastfolder = value; 
			} 
		}

		/// <summary>
		/// Indicates whether items in the feed should be marked as read on exiting
		/// the feed in the UI.
		/// </summary>
		private bool markitemsreadonexit;

		/// <summary>
		/// Gets or sets whether items in the feed should be marked as read on exiting
		/// the feed in the UI
		/// </summary>
		public bool MarkItemsReadOnExit{
			get{ return this.markitemsreadonexit;}  
			
			set{ this.markitemsreadonexit = value; } 		
		}

		/// <summary>
		/// Indicates whether enclosures should be downloaded in the background.
		/// </summary>
		private bool downloadenclosures;
		
		/// <summary>
		/// Gets or sets whether enclosures should be downloaded in the background
		/// </summary>
		public  bool DownloadEnclosures { 
			get{ return this.downloadenclosures;}  
			
			set{ this.downloadenclosures = value; } 
		}


		/// <summary>
		/// Indicates the maximum amount of space that enclosures and podcasts can use on disk.
		/// </summary>
		private int enclosurecachesize = Int32.MaxValue;


		/// <summary>
		/// Indicates the maximum amount of space that enclosures and podcasts can use on disk.
		/// </summary>
		public  int EnclosureCacheSize { 
			get{ return this.enclosurecachesize;}  
			
			set{ this.enclosurecachesize = value; } 
		}

		/// <summary>
		/// Indicates the number of enclosures which should be downloaded automatically from a newly subscribed feed.
		/// </summary>
		private int numtodownloadonnewfeed = Int32.MaxValue;


		/// <summary>
		/// Indicates the number of enclosures which should be downloaded automatically from a newly subscribed feed.
		/// </summary>
		public  int NumEnclosuresToDownloadOnNewFeed { 
			get{ return this.numtodownloadonnewfeed;}  
			
			set{ this.numtodownloadonnewfeed = value; } 
		}


		/// <summary>
		/// Indicates whether podcasts and enclosures should be downloaded to a folder 
		/// named after the feed. 
		/// </summary>
		private bool createsubfoldersforenclosures;
		
		/// <summary>
		/// Gets or sets whether  podcasts and enclosures should be downloaded to a folder 
		/// named after the feed
		/// </summary>
		public  bool CreateSubfoldersForEnclosures { 
			get{ return this.createsubfoldersforenclosures;}  
			
			set{ this.createsubfoldersforenclosures = value; } 
		}

		/// <summary>
		/// Indicates whether enclosures should be downloaded in the background.
		/// </summary>
		private bool enclosurealert;
		
		/// <summary>
		/// Gets or sets whether a toast windows should be displayed on a successful download
		/// of an enclosure.
		/// </summary>
		public  bool EnclosureAlert { 
			get{ return this.enclosurealert;}  
			
			set{ this.enclosurealert = value; } 
		}

		/// <summary>
		/// Indicates which properties of a NewsItem should be made columns in the RSS Bandit listview
		/// </summary>
		private string listviewlayout;
		
		/// <summary>
		/// Gets or sets wwhich properties of a NewsItem should be made columns in the RSS Bandit listview
		/// </summary>
		public  string FeedColumnLayout { 
			get{ return this.listviewlayout;}  
			
			set{ this.listviewlayout = value; } 
		}

		#region HTTP UserAgent 
		/// <summary>
		/// Our default short HTTP user agent string
		/// </summary>
		public const string DefaultUserAgent = "NewsHandler 1.1"; 

		/// <summary>
		/// A template string to assamble a unified user agent string.
		/// </summary>
		private static string userAgentTemplate;

		/// <summary>
		/// global long HTTP user agent string
		/// </summary>
		private static string globalLongUserAgent;

		/// <summary>
		/// Build a full user agent string incl. OS and .NET version 
		/// from the provided userAgent
		/// </summary>
		/// <param name="userAgent">string</param>
		/// <returns>The long HTTP user agent string</returns>
		public static string UserAgentString(string userAgent) {
			if (StringHelper.EmptyOrNull(userAgent))
				return GlobalUserAgentString;
			return String.Format(userAgentTemplate, userAgent); 
		}
		/// <summary>
		/// Returns a global long HTTP user agent string build from the
		/// instance setting. 
		/// To be used by sub-components that do not have a instance variable 
		/// of the NewsHandler.
		/// </summary>
		public static string GlobalUserAgentString {
			get { 
				if (null == globalLongUserAgent)
					globalLongUserAgent = UserAgentString(DefaultUserAgent);
				return globalLongUserAgent; 
			}
		}

		/// <summary>
		/// The short HTTP user agent string used when requesting feeds
		/// and the property was not set via 
		/// </summary>
		private string useragent = DefaultUserAgent; 

		/// <summary>
		/// The short HTTP user agent string used when requesting feeds. 
		/// </summary>
		public string UserAgent{ 
			get { return useragent;		} 
			set { 
				useragent = value; 	
				globalLongUserAgent = UserAgentString(useragent);
			}		
		}

		/// <summary>
		/// The long HTTP user agent string used when requesting feeds. 
		/// </summary>
		public string FullUserAgent{ 
			get { return UserAgentString(this.UserAgent);	} 
		}
		
		#endregion

		/// <summary>
		/// FeedsCollection representing subscribed feeds list
		/// </summary>
		private FeedsCollection _feedsTable = new FeedsCollection();  

		/// <summary>
		/// Represents the list of available categories for feeds. 
		/// </summary>
		private CategoriesCollection categories = new CategoriesCollection(); 

		/// <summary>
		/// Represents the list of available feed column layouts for feeds. 
		/// </summary>
		private FeedColumnLayoutCollection layouts = new FeedColumnLayoutCollection(); 


		/// <summary>
		/// Hashtable representing downloaded feed items
		/// </summary>
		private Hashtable itemsTable = new Hashtable();  

		/// <summary>
		/// Collection contains NntpServerDefinition objects.
		/// Keys are the account name(s) - friendly names for the news server def.:
		/// NntpServerDefinition.Name's
		/// </summary>
		private ListDictionary nntpServers = new ListDictionary();
		/// <summary>
		/// Collection contains UserIdentity objects.
		/// Keys are the UserIdentity.Name's
		/// </summary>
		private ListDictionary identities = new ListDictionary();

		#region delegates/events/argument classes
		/// <summary>
		/// The callback used within the BeforeDownloadFeedStarted event.
		/// </summary>
		public delegate void DownloadFeedStartedCallback(object sender, DownloadFeedCancelEventArgs e);
		/// <summary>
		/// The event that will be invoked on clients to notify them that 
		/// when a feed starts to be downloaded (AsyncWebRequest). 
		/// </summary>
		public event DownloadFeedStartedCallback BeforeDownloadFeedStarted = null;

		/// <summary>
		/// BeforeDownloadFeedStarted event argument class.
		/// </summary>
		[ComVisible(false)]
			public class DownloadFeedCancelEventArgs: System.ComponentModel.CancelEventArgs {
			/// <summary>
			/// Class initializer.
			/// </summary>
			/// <param name="feed">feed Uri</param>
			/// <param name="cancel">bool, set to true, if you want to cancel further processing</param>
			public DownloadFeedCancelEventArgs(Uri feed, bool cancel):base(cancel) {
				this.feedUri = feed;
			}
			private Uri feedUri;
			/// <summary>
			/// The related feed Uri.
			/// </summary>
			public Uri FeedUri { get { return feedUri; } }
		}
	  
		/// <summary>
		/// Callback delegate used on event OnUpdatedFeed.
		/// </summary>
		public delegate void UpdatedFeedCallback(object sender, UpdatedFeedEventArgs e);
		/// <summary>
		/// Event called on every updated feed.
		/// </summary>
		public event UpdatedFeedCallback OnUpdatedFeed = null;


		/// <summary>
		/// Callback delegate used on event OnDownloadedEnclosure.
		/// </summary>
		public delegate void DownloadedEnclosureCallback(object sender, DownloadItemEventArgs e);
		
		/// <summary>
		/// Event called on every completed enclosure download. 
		/// </summary>
		public event DownloadedEnclosureCallback OnDownloadedEnclosure = null;

		/// <summary>
		/// Callback delegate used on event OnUpdatedFavicon.
		/// </summary>
		public delegate void UpdatedFaviconCallback(object sender, UpdatedFaviconEventArgs e);
		/// <summary>
		/// Event called on every updated favicon.
		/// </summary>
		public event UpdatedFaviconCallback OnUpdatedFavicon = null;


		/// <summary>
		/// OnUpdatedFavicon event argument class.
		/// </summary>
		public class UpdatedFaviconEventArgs: EventArgs {
		
			/// <summary>
			/// Called on every updated favicon.
			/// </summary>
			/// <param name="favicon"> The name of the favicon file</param> 
			/// <param name="feedUrls">The list of URLs that will utilize this favicon</param>		
			public UpdatedFaviconEventArgs(string favicon, StringCollection feedUrls) {
				this.favicon = favicon;
				this.feedUrls = feedUrls;			
			}

			private string favicon;
			/// <summary>
			/// The name of the favicon file. 
			/// </summary>
			public string Favicon { get{ return this.favicon;} }

			private StringCollection feedUrls; 
			/// <summary>
			/// The URLs of the feeds that will utilize this favicon. 
			/// </summary>
			public StringCollection FeedUrls{ get{ return this.feedUrls;} }				
		}

		/// <summary>
		/// OnUpdatedFeed event argument class.
		/// </summary>
		public class UpdatedFeedEventArgs: EventArgs {
			/// <summary>
			/// Called on every updated feed.
			/// </summary>
			/// <param name="requestUri">Original requested Uri of the feed</param>
			/// <param name="newUri">The (maybe) new feed location. This could be set on a redirect or other mechanism.
			/// If the location was not changed, this parameter is left null</param>
			/// <param name="result">If result is <c>NotModified</c>, the conditional GET succeeds and no items are returned.</param>
			/// <param name="priority">Priority of the request</param>
			/// <param name="firstSuccessfulDownload">Indicates whether this is the first time the feed has been successfully downloaded
			/// to the cache</param>
			public UpdatedFeedEventArgs(Uri requestUri, Uri newUri,  RequestResult result, int priority, bool firstSuccessfulDownload) {
				this.requestUri = requestUri;
				this.newUri = newUri;				
				this.result = result;
				this.priority = priority;
				this.firstSuccessfulDownload = firstSuccessfulDownload; 
			}
			private Uri requestUri, newUri;
			/// <summary>
			/// Uri of the feed, that was updated
			/// </summary>
			public Uri UpdatedFeedUri { get { return requestUri; } }	// should return Clone() ?
			/// <summary>
			/// Uri of the feed, if it was moved on the Web to a new location.
			/// </summary>
			public Uri NewFeedUri { get { return newUri; } }				// should return Clone() ?
			
			private RequestResult result;
			/// <summary>
			/// RequestResult: OK or NotModified
			/// </summary>
			public RequestResult UpdateState { get { return result; } }
			private int priority;
			/// <summary>
			/// Gets the queued priority
			/// </summary>
			public int Priority { get { return priority; } }

			private bool firstSuccessfulDownload; 

			/// <summary>
			/// Indicates whether this is the first time the feed has been downloaded to 
			/// the cache. 
			/// </summary>
			public bool FirstSuccessfulDownload { get { return firstSuccessfulDownload; } }

		}

		/// <summary>
		/// Callback delegate used for event OnUpdateFeedException
		/// </summary>
		public delegate void UpdateFeedExceptionCallback(object sender, UpdateFeedExceptionEventArgs e);
		/// <summary>
		/// Event called, if the WebRequest fails with any exception.
		/// </summary>
		public event UpdateFeedExceptionCallback OnUpdateFeedException = null;

		/// <summary>
		/// Event argument class used in OnUpdateFeedException.
		/// </summary>
		public class UpdateFeedExceptionEventArgs: EventArgs {
			/// <summary>
			/// Initializer
			/// </summary>
			/// <param name="requestUri">feed Uri, that was requested</param>
			/// <param name="e">Exception caused by the request</param>
			/// <param name="priority">int</param>
			public UpdateFeedExceptionEventArgs(string requestUri, Exception e, int priority) {
				this.requestUri = requestUri;
				this.exception = e;
				this.priority = priority;
			}
			private string requestUri;
			/// <summary>
			/// feed Uri.
			/// </summary>
			public string FeedUri { get { return requestUri; } 	}	

			private Exception exception;
			/// <summary>
			/// caused exception
			/// </summary>
			public Exception ExceptionThrown { get { return exception; } }
			private int priority;
			/// <summary>
			/// Gets the queued priority
			/// </summary>
			public int Priority { get { return priority; } }
		}

		/// <summary>
		/// UpdateFeedsStarted event argument class. Multiple feeds update.
		/// </summary>
		public class UpdateFeedsEventArgs: EventArgs {
			/// <summary>
			/// Initializer
			/// </summary>
			/// <param name="forced">true, if it was a forced (manually initiated) request</param>
			public UpdateFeedsEventArgs(bool forced) {
				this.forced = forced;
			}
			private bool forced;
			/// <summary>
			/// True, if it was a manually forced request
			/// </summary>
			public bool ForcedRefresh { get { return forced; } }	
		}

		/// <summary>
		/// UpdateFeedStarted event argument class. Single feed update.
		/// </summary>
		public class UpdateFeedEventArgs: UpdateFeedsEventArgs {
			/// <summary>
			/// Initializer
			/// </summary>
			/// <param name="feed">feed Uri</param>
			/// <param name="forced">true, if it was a forced (manually initiated) request</param>
			/// <param name="priority">Priority of the request</param>
			public UpdateFeedEventArgs(Uri feed, bool forced, int priority):base(forced) {
				this.feedUri = feed;
				this.priority = priority;
			}
			private Uri feedUri;
			/// <summary>
			/// Feed Uri.
			/// </summary>
			public Uri FeedUri { get { return feedUri; } }

			private int priority;
			/// <summary>
			/// Gets the queued priority
			/// </summary>
			public int Priority { get { return priority; } }
		}

		/// <summary>
		/// Delegate used for UpdateFeedsStarted event.
		/// </summary>
		public delegate void UpdateFeedsStartedHandler(object sender, UpdateFeedsEventArgs e);
		/// <summary>
		/// Called if RefreshFeeds() was initiated (all feeds).
		/// </summary>
		public event UpdateFeedsStartedHandler UpdateFeedsStarted = null;

		/// <summary>
		/// Delegate used for UpdateFeedStarted event.
		/// </summary>
		public delegate void UpdateFeedStartedHandler(object sender, UpdateFeedEventArgs e);
		/// <summary>
		/// Called as each individual feed start to refresh
		/// </summary>
		public event UpdateFeedStartedHandler UpdateFeedStarted = null;

		/// <summary>
		/// Called if all async. requests are done.
		/// </summary>
		public event System.EventHandler OnAllAsyncRequestsCompleted = null;

		//Search	impl. 

		/// <summary>Signature for <see cref="NewsItemSearchResult">NewsItemSearchResult</see>  event</summary>
		public delegate void NewsItemSearchResultEventHandler(object sender, NewsItemSearchResultEventArgs e); 
		/// <summary>Signature for <see cref="FeedSearchResult">FeedSearchResult</see>  event</summary>
		public delegate void FeedSearchResultEventHandler(object sender, FeedSearchResultEventArgs e); 
		/// <summary>Signature for <see cref="SearchFinished">SearchFinished</see>  event</summary>
		public delegate void SearchFinishedEventHandler(object sender, SearchFinishedEventArgs e);

		/// <summary>Called if NewsItems are found, that match the search criteria(s)</summary>
		public event NewsItemSearchResultEventHandler NewsItemSearchResult; 
		/// <summary>Called if feedsFeed(s) are found, that match the search criteria(s)</summary>
		public event FeedSearchResultEventHandler FeedSearchResult; 
		/// <summary>Called on a search finished</summary>
		public event SearchFinishedEventHandler SearchFinished;

		/// <summary>
		/// Contains the search result, if feedsFeed's are found. Used on FeedSearchResult event.
		/// </summary>
		[ComVisible(false)]
			public class FeedSearchResultEventArgs : System.ComponentModel.CancelEventArgs {
			/// <summary>
			/// Initializer
			/// </summary>
			/// <param name="f">feedsFeed</param>
			/// <param name="tag">object, used by the caller only</param>
			/// <param name="cancel">true, if the search request should be cancelled</param>
			public FeedSearchResultEventArgs (
				feedsFeed f, object tag, bool cancel):base(cancel) {
				this.Feed = f; this.Tag = tag;
			}
			/// <summary>
			/// feedsFeed.
			/// </summary>
			public feedsFeed Feed;
			/// <summary>
			/// Object used by the caller only
			/// </summary>
			public object Tag;
		}

		/// <summary>
		/// Contains the search result, if NewsItem's are found. Used on NewsItemSearchResult event.
		/// </summary>
		[ComVisible(false)]
			public class NewsItemSearchResultEventArgs: System.ComponentModel.CancelEventArgs {
			/// <summary>
			/// Initializer
			/// </summary>
			/// <param name="items">ArrayList of NewsItems</param>
			/// <param name="tag">Object used by caller</param>
			/// <param name="cancel"></param>
			public NewsItemSearchResultEventArgs(
				ArrayList items, object tag, bool cancel):base(cancel) {
				this.NewsItems = items;
				this.Tag = tag;
			}
			/// <summary>
			/// NewsItem list
			/// </summary>
			public ArrayList NewsItems;
			/// <summary>
			/// Object used by caller
			/// </summary>
			public object Tag;
		}

		/// <summary>
		/// Provide informations about a finished search. Used on SearchFinished event.
		/// </summary>
		public class SearchFinishedEventArgs : EventArgs {
			/// <summary>
			/// Initializer
			/// </summary>
			/// <remarks>This modifies the input FeedInfoList by replacing its NewsItem contents 
			/// with SearchHitNewsItems</remarks>
			/// <param name="tag">Object used by caller</param>
			/// <param name="matchingFeeds"></param>
			/// <param name="matchingFeedsCount">integer stores the count of matching feeds</param>
			/// <param name="matchingItemsCount">integer stores the count of matching NewsItem's (over all feeds)</param>
			public SearchFinishedEventArgs (
				object tag, FeedInfoList matchingFeeds, int matchingFeedsCount, int matchingItemsCount):
					this(tag, matchingFeeds, new ArrayList(), matchingFeedsCount, matchingItemsCount) {

				ArrayList temp = new ArrayList();

				foreach(FeedInfo fi in matchingFeeds){
					 
					foreach(NewsItem ni in fi.ItemsList){
					
						if(ni is SearchHitNewsItem)
							temp.Add(ni);
						else
							temp.Add(new SearchHitNewsItem(ni));
					}
					fi.ItemsList.Clear(); 
					fi.ItemsList.AddRange(temp); 
					this.MatchingItems.AddRange(temp); 
					temp.Clear(); 
				}//foreach
			}

			/// <summary>
			/// Initializer
			/// </summary>
			/// <param name="tag">Object used by caller</param>
			/// <param name="matchingFeeds">The matching feeds.</param>
			/// <param name="matchingNewsItems">The matching news items.</param>
			/// <param name="matchingFeedsCount">integer stores the count of matching feeds</param>
			/// <param name="matchingItemsCount">integer stores the count of matching NewsItem's (over all feeds)</param>
			public SearchFinishedEventArgs (
				object tag, FeedInfoList matchingFeeds, ArrayList matchingNewsItems, int matchingFeedsCount, int matchingItemsCount):base() {
				this.MatchingFeedsCount= matchingFeedsCount;
				this.MatchingItemsCount= matchingItemsCount;
				this.MatchingFeeds = matchingFeeds;
				this.MatchingItems = matchingNewsItems;
				this.Tag = tag;				
			}
			/// <summary></summary>
			public readonly int MatchingFeedsCount;
			/// <summary></summary>
			public readonly int MatchingItemsCount;
			/// <summary></summary>
			public readonly object Tag;
			/// <summary></summary>
			public readonly FeedInfoList MatchingFeeds;
			/// <summary></summary>
			public readonly ArrayList MatchingItems;
		}

		#endregion

		private const int maxItemsPerSearchResult = 10;


		private ArrayList SearchNewsItemsHelper(ArrayList prevMatchItems, SearchCriteriaCollection criteria, FeedDetailsInternal fi, FeedInfo fiMatchedItems,  ref int itemmatches, ref int feedmatches, object tag){
		  
			ArrayList matchItems = new ArrayList(maxItemsPerSearchResult);
			matchItems.AddRange(prevMatchItems); 
			bool cancel = false; 
			bool feedmatch = false; 
		  
			foreach(NewsItem item in fi.ItemsList){
				if(criteria.Match(item)){
					//_log.Info("MATCH FOUND: " + item.Title);  
					feedmatch = true; 
					matchItems.Add(item); 
					fiMatchedItems.ItemsList.Add(item);
					itemmatches++;
					if ((itemmatches % 50) == 0) { //Caller return results On the last feed we found results 
						cancel = RaiseNewsItemSearchResultEvent(matchItems, tag);
						matchItems.Clear();
					}
					if (cancel) throw new InvalidOperationException("SEARCH CANCELLED");
				}
			}//foreach(NewsItem...)

			if(feedmatch) feedmatches++; 

			return matchItems; 
		}

		/// <summary>
		/// Search for NewsItems, that match a provided criteria collection within a optional search scope.
		/// </summary>
		/// <param name="criteria">SearchCriteriaCollection containing the defined search criteria</param>
		/// <param name="scope">Search scope: an array of feedsFeed</param>
		/// <param name="tag">optional object to be used by the caller to identify this search</param>
		/// <param name="cultureName">Name of the culture.</param>
		/// <param name="returnFullItemText">if set to <c>true</c>, full item texts are returned instead of the summery.</param>
		public void SearchNewsItems(SearchCriteriaCollection criteria, feedsFeed[] scope, object tag, string cultureName, bool returnFullItemText) {
		
			// if scope is an empty array: search all, else search only in spec. feeds
			int feedmatches = 0;
			int itemmatches = 0;

			ArrayList unreturnedMatchItems = new ArrayList(); 
			FeedInfoList fiList = new FeedInfoList(String.Empty); 			
		  
			Exception ex = null;
			bool valid = this.SearchHandler.ValidateSearchCriteria(criteria, cultureName, out ex);

			if (ex != null)	// report always any error (warnings)
			{
				// render the error in-line (search result):
				fiList.Add((FeedInfo)CreateHelpNewsItemFromException(ex).FeedDetails);
				feedmatches = fiList.Count;
				unreturnedMatchItems = fiList.GetAllNewsItems();
				itemmatches = unreturnedMatchItems.Count;
			}

			if (valid) {
				try {
					// do the search (using lucene):
					LuceneSearch.Result r = this.SearchHandler.ExecuteSearch(criteria, scope, cultureName);
				
					// we iterate r.ItemsMatched to build a
					// NewsItemIdentifier and ArrayList list with items, that
					// match the read status (if this was a search criteria)
					// then call FindNewsItems(NewsItemIdentifier[]) to get also
					// the FeedInfoList.
					// Raise ONE event, instead of two to return all (counters, lists)
				
					SearchCriteriaProperty criteriaProperty = null;
					foreach (ISearchCriteria sc in criteria) {
						criteriaProperty = sc as SearchCriteriaProperty;
						if (criteriaProperty != null && 
						    PropertyExpressionKind.Unread == criteriaProperty.WhatKind) 
							break;
					}
				
				
					ItemReadState readState = ItemReadState.Ignore; 
					if (criteriaProperty != null) {
						if (criteriaProperty.BeenRead)
							readState = ItemReadState.BeenRead;
						else
							readState = ItemReadState.Unread;
					}

				
					if (r != null && r.ItemMatchCount > 0) {	// append results
					
						SearchHitNewsItem[] nids = new SearchHitNewsItem[r.ItemsMatched.Count];
						r.ItemsMatched.CopyTo(nids, 0);
						fiList.AddRange(FindNewsItems(nids, readState, returnFullItemText));
						feedmatches = fiList.Count;

						unreturnedMatchItems = fiList.GetAllNewsItems();
						itemmatches = unreturnedMatchItems.Count;
					} 
				
				}catch (Exception searchEx) {
					// render the error in-line (search result):
					fiList.Add((FeedInfo)CreateHelpNewsItemFromException(searchEx).FeedDetails);
					feedmatches = fiList.Count;
					unreturnedMatchItems = fiList.GetAllNewsItems();
					itemmatches = unreturnedMatchItems.Count;
				}
			}
			
			RaiseSearchFinishedEvent(tag, fiList,  unreturnedMatchItems , feedmatches, itemmatches); 

		}
		
		/// <summary>
		/// Builds a ExceptionalNewsItem from a exception.
		/// This way it can be displayed in-line with a search result or
		/// a normal feed to get the user the hint in the news item list.
		/// to provide help about the error.
		/// </summary>
		/// <param name="e">Exception</param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException">If e is null</exception>
		private ExceptionalNewsItem CreateHelpNewsItemFromException(Exception e)
		{
			if (e == null)
				throw new ArgumentNullException("e");
			
			feedsFeed f = new feedsFeed();
			f.link = "http://www.rssbandit.org/docs/";	//?? what to specify here?
			f.title = ComponentsText.ExceptionHelpFeedTitle;

			ExceptionalNewsItem newsItem = new ExceptionalNewsItem(f, ComponentsText.ExceptionHelpFeedItemTitle(e.GetType().Name), 
				(e.HelpLink != null ? e.HelpLink : "http://www.rssbandit.org/docs/"), 
				e.Message, e.Source, DateTime.Now.ToUniversalTime(), Guid.NewGuid().ToString());

			newsItem.Subject       = e.GetType().Name; 
			newsItem.CommentStyle  = SupportedCommentStyle.None; 
			newsItem.Enclosures    = GetArrayList.Empty; 
			newsItem.WatchComments = false; 
			newsItem.Language      = CultureInfo.CurrentUICulture.Name;
			newsItem.HasNewComments = false;
			
			FeedInfo fi = new FeedInfo(f.id, f.cacheurl, new ArrayList(new NewsItem[]{newsItem}),
				f.title, f.link, ComponentsText.ExceptionHelpFeedDesc, new Hashtable(1), newsItem.Language);
			newsItem.FeedDetails = fi;
			return newsItem;
		}

		/// <summary>
		/// Search for NewsItems, that match a provided criteria collection within a optional search scope.
		/// </summary>
		/// <param name="criteria">SearchCriteriaCollection containing the defined search criteria</param>
		/// <param name="scope">Search scope: an array of feedsFeed</param>
		/// <param name="tag">optional object to be used by the caller to identify this search</param>
		public void SearchNewsItems(SearchCriteriaCollection criteria, feedsFeed[] scope, object tag) {
			// if scope is an empty array: search all, else search only in spec. feeds
			int feedmatches = 0;
			int itemmatches = 0;
			int feedcounter = 0;

			ArrayList unreturnedMatchItems = new ArrayList(); 
			FeedInfo[] feedInfos;
			FeedInfoList fiList = new FeedInfoList(String.Empty); 			
		  
			try{

				if(scope.Length == 0){
					// we search a copy of the current content to prevent the lock(itemsTable)
					// while we do the more time consuming search ops. New received items are
					// automatically recognized to be searched as they are float into the system.
					lock(itemsTable) { 
						feedInfos = new FeedInfo[itemsTable.Count];
						itemsTable.Values.CopyTo(feedInfos, 0);
					}
					foreach(FeedInfo fi in feedInfos){
		
						FeedInfo fiClone = fi.Clone(false);
						//fiClone.ItemsList.Clear(); 

						unreturnedMatchItems = SearchNewsItemsHelper(unreturnedMatchItems, criteria, fi, fiClone, ref itemmatches, ref feedmatches, tag); 				  
						feedcounter++;

						if ((feedcounter % 5) == 0) {	// to shorten search if user want to cancel. Above modulo will only stop if it founds at least 100 matches...
							bool cancel = RaiseNewsItemSearchResultEvent(unreturnedMatchItems, tag);
							unreturnedMatchItems.Clear();
							if (cancel) 
								break;
						}

						if(fiClone.ItemsList.Count != 0){
							fiList.Add(fiClone); 
						}

					}//foreach(FeedInfo...)

				}else{
		  
					lock(itemsTable) { 
						feedInfos = new FeedInfo[scope.Length];
						for (int i = 0; i < scope.Length; i++){
							feedInfos[i] = (FeedInfo) itemsTable[scope[i].link];
						}
					}
		  
					foreach(FeedInfo fi in feedInfos){
						if(fi != null){
							
							FeedInfo fiClone = fi.Clone(false);
							//fiClone.ItemsList.Clear();

							unreturnedMatchItems = SearchNewsItemsHelper(unreturnedMatchItems, criteria, fi, fiClone, ref itemmatches, ref feedmatches, tag); 
							feedcounter++;

							if ((feedcounter % 5) == 0) {	// to shorten search if user want to cancel. Above modulo will only stop if it founds at least 100 matches...
								bool cancel = RaiseNewsItemSearchResultEvent(unreturnedMatchItems, tag);
								unreturnedMatchItems.Clear();
								if (cancel) 
									break;
							}

							if(fiClone.ItemsList.Count != 0){
								fiList.Add(fiClone); 
							}

						}
					}

				}
			
				if(unreturnedMatchItems.Count > 0){
					RaiseNewsItemSearchResultEvent(unreturnedMatchItems, tag);
				}

			}catch(InvalidOperationException ioe){// New feeds added to FeedsTable from another thread  
				Trace("SearchNewsItems() casued InvalidOperationException: {0}", ioe);
			} 
	
			RaiseSearchFinishedEvent(tag, fiList, feedmatches, itemmatches); 
		}

		/// <summary>
		/// Initiate a remote (web) search using the engine incl. search expression specified
		/// by searchFeedUrl. We assume, the specified Url will return a RSS feed.
		/// This can be used e.g. to get a RSS search result from feedster.
		/// </summary>
		/// <param name="searchFeedUrl">Complete Url of the search engine incl. search expression</param>
		/// <param name="tag">optional, can be used by the caller</param>
		public void SearchRemoteFeed(string searchFeedUrl, object tag) {
			int feedmatches = 0;
			int itemmatches = 0;

			ArrayList unreturnedMatchItems = this.GetItemsForFeed(searchFeedUrl); 
			RaiseNewsItemSearchResultEvent(unreturnedMatchItems, tag);
			feedmatches = 1;
			itemmatches = unreturnedMatchItems.Count;
			FeedInfo fi = new FeedInfo(String.Empty, String.Empty, unreturnedMatchItems, String.Empty, String.Empty, String.Empty, new Hashtable(), String.Empty); 			
			FeedInfoList fil = new FeedInfoList(String.Empty); 
			fil.Add(fi);
			RaiseSearchFinishedEvent(tag, fil, feedmatches, itemmatches); 

		}

		/// <summary>
		/// [To be provided]
		/// </summary>
		/// <param name="criteria"></param>
		/// <param name="scope"></param>
		/// <param name="tag"></param>
		public void SearchFeeds(SearchCriteriaCollection criteria, feedsFeed[] scope, object tag) {
			// if scope is an empty array: search all, else search only in spec. feeds
			// pseudo code:
			/* int matches = 0;
			foreach (feedsFeed f in _feedsTable) {
				if (criteria.Match(f)) {
					matches++;
					if (RaiseFeedSearchResultEvent(f, tag))
					  break;
				}
			}
			RaiseSearchFinishedEvent(tag, matches, 0); */

			throw new NotSupportedException(); 
		}
	  
		private bool RaiseNewsItemSearchResultEvent(ArrayList matchItems, object tag) {
			try {
				if (NewsItemSearchResult != null) {
					NewsItemSearchResultEventArgs ea = new NewsItemSearchResultEventArgs(new ArrayList(matchItems), tag, false);
					NewsItemSearchResult(this, ea);
					return ea.Cancel;
				}
			} catch {}
			return false;
		}
		// not currently used:
		private bool RaiseFeedSearchResultEvent(feedsFeed f, object tag) {
			try {
				if (FeedSearchResult != null) {
					FeedSearchResultEventArgs ea = new FeedSearchResultEventArgs(f, tag, false);
					FeedSearchResult(this, ea);
					return ea.Cancel;
				}
			} catch {}
			return false;
		}
		private void RaiseSearchFinishedEvent(object tag, FeedInfoList matchingFeeds, int matchingFeedsCount, int matchingItemsCount) {
			try {
				if (SearchFinished != null) {
					SearchFinished(this, new SearchFinishedEventArgs(tag, matchingFeeds, matchingFeedsCount, matchingItemsCount ));
				}
			} catch (Exception e) { 
				Trace("SearchFinished() event code raises exception: {0}",e);
			}
		}
		private void RaiseSearchFinishedEvent(object tag, FeedInfoList matchingFeeds, ArrayList matchingItems, int matchingFeedsCount, int matchingItemsCount) {
			try {
				if (SearchFinished != null) {
					SearchFinished(this, new SearchFinishedEventArgs(tag, matchingFeeds, matchingItems, matchingFeedsCount, matchingItemsCount ));
				}
			} catch (Exception e) { 
				Trace("SearchFinished() event code raises exception: {0}",e);
			}
		}

		/// <summary>
		/// Retrieves a specified NewsItem given the identifying feed URL and Item ID
		/// </summary>
		/// <param name="nid">The value used to identify the NewsItem</param>
		/// <returns>The NewsItem or null if it could not be found</returns>
		public NewsItem FindNewsItem(SearchHitNewsItem nid){

			if(nid != null){

				FeedInfo fi   = this.itemsTable[nid.FeedLink] as FeedInfo; 

				if(fi != null){
					ArrayList items = fi.ItemsList.Clone() as ArrayList; 
				
					foreach(NewsItem ni in items){
						if(ni.Id.Equals(nid.Id)){
							return ni;
						}
					}//foreach
				}//if(fi != null)
			
			}//if(nid != null)

			return null; 		
		}


		/// <summary>
		/// Retrieves a list of NewsItems and their FeedInfo objects
		/// not regarding their read states.
		/// </summary>
		/// <param name="nids">The values used to identify the NewsItems</param>
		/// <returns>The list of FeedInfo objects containing the NewsItems (content summaries)</returns>
		public FeedInfoList FindNewsItems(SearchHitNewsItem[] nids){
			return this.FindNewsItems(nids, ItemReadState.Ignore, false); 
		}



		/// <summary>
		/// Retrieves a list of NewsItems and their FeedInfo objects
		/// </summary>
		/// <param name="nids">The values used to identify the NewsItems</param>
		/// <param name="readState">Indicates how to interpret read state of NewsItems to return</param>
		/// <param name="returnFullItemText">if set to <c>true</c> we load/return full item texts.</param>
		/// <returns>
		/// The list of FeedInfo objects containing the NewsItems
		/// </returns>
		public FeedInfoList FindNewsItems(SearchHitNewsItem[] nids, ItemReadState readState, bool returnFullItemText){

			FeedInfoList fiList     = new FeedInfoList(String.Empty); 	
			Hashtable matchedFeeds  = new Hashtable(); 
			Hashtable itemlists     = new Hashtable();
			
			foreach(SearchHitNewsItem nid in nids){
			
				FeedInfo fi= null, originalfi   = this.itemsTable[nid.FeedLink] as FeedInfo; 
				ArrayList items = null; 

				if(originalfi != null){

					if(matchedFeeds.ContainsKey(nid.FeedLink)){
						fi = matchedFeeds[nid.FeedLink] as FeedInfo; 
						items = itemlists[nid.FeedLink] as ArrayList; 
					}else{
						fi = originalfi.Clone(false); 
						items = originalfi.ItemsList.Clone() as ArrayList; 
						matchedFeeds.Add(nid.FeedLink, fi); 
						itemlists.Add(nid.FeedLink, items); 
					}
				
					bool beenRead = (readState == ItemReadState.BeenRead);
					foreach(NewsItem ni in items){
						if(ni.Id.Equals(nid.Id)){
							if (readState == ItemReadState.Ignore || 
								ni.BeenRead == beenRead){
								nid.BeenRead = ni.BeenRead; //copy over read state
								if (returnFullItemText && !nid.HasContent)
									this.GetCachedContentForItem(nid);
								fi.ItemsList.Add(nid);
								nid.FeedDetails = fi; 
							}
							break;

						}
					}//foreach
				}//if(fi != null)

			}			

			foreach(FeedInfo f in matchedFeeds.Values){
				
				//Ensure that we actually matched items from the feed before adding it. 
				//This can happen if search index has items that are no longer in RSS 
				//feed cache. 
				if(f.ItemsList.Count > 0){ 
					fiList.Add(f); 
				}
			}

			return fiList; 		
		}

		/// <summary>
		/// The Application Name or ID that uses the component. This will be used to 
		/// initialize the user path to store the feeds file and cached items.
		/// </summary>
		internal string applicationName = "NewsComponents";


		/// <summary>
		/// Accesses the list of user specified layouts (currently listview only) 
		/// </summary>
		public FeedColumnLayoutCollection ColumnLayouts{ 
		
			get { 
				
				if(layouts== null){				
					layouts = new FeedColumnLayoutCollection();
				}							
				
				return layouts;
			}			
		
		}

		/// <summary>
		/// The string used to build categories hierarchy
		/// </summary>
		public static string CategorySeparator = @"\";

		/// <summary>
		/// Accesses the list of user specified categories used for organizing 
		/// feeds. 
		/// </summary>
		public CategoriesCollection Categories{ 
		
			get { 
				
				if(categories== null){				
					categories = new CategoriesCollection();
				}							
				
				return categories;
			}			
		
		}

		/// <summary>
		/// Accesses the table of RSS feed objects. 
		/// </summary>
		/// <exception cref="InvalidOperationException">If some error occurs on converting 
		/// XML feed list to feed table</exception>

		public FeedsCollection FeedsTable{
		
			//		[MethodImpl(MethodImplOptions.Synchronized)]
			get{
				if(!validationErrorOccured){ 
					return _feedsTable; 										
				}else {
					return null;
				}
			}
		
		}

		/// <summary>
		/// Accesses the list of NntpServerDefinition objects 
		/// Keys are the account name(s) - friendly names for the news server def.:
		/// NewsServerDefinition.Name's
		/// </summary>
		public IDictionary NntpServers { 
		
			[DebuggerStepThrough()]
			get { 
				
				if(this.nntpServers== null){				
					this.nntpServers = new ListDictionary();
				}							
				
				return this.nntpServers;
			}			
		
		}

		/// <summary>
		/// Accesses the list of UserIdentity objects.
		/// Keys are the UserIdentity.Name's
		/// </summary>
		public IDictionary UserIdentity { 
		
			[DebuggerStepThrough()]
			get { 
				
				if(this.identities== null){				
					this.identities = new ListDictionary();
				}							
				
				return this.identities;
			}			
		
		}

		/// <summary>
		/// How often feeds are refreshed by default if no specific rate specified by the feed. 
		/// The value is specified in milliseconds. 
		/// </summary>
		/// <remarks>By default this value is set to one hour. </remarks>
		private int refreshrate = 60 * 60 * 1000; 

		/// <summary>
		///  How often feeds are refreshed by default if no specific rate specified by the feed. 
		///  Setting this property resets the refresh rate for all feeds. 
		/// </summary>
		/// <remarks>If set to a negative value then the old value remains. Setting the 
		/// value to zero means feeds are no longer updated.</remarks>
		public int RefreshRate {
		
			set {
				if(value >= 0){
					this.refreshrate = value; 
				}

				string[] keys;
			
				lock (FeedsTable.SyncRoot) {
					keys = new string[FeedsTable.Count];
					if (FeedsTable.Count > 0)
						FeedsTable.Keys.CopyTo(keys, 0);	
				}
				
				for(int i = 0, len = keys.Length; i < len; i++){
					FeedsTable[keys[i]].refreshrate = this.refreshrate; 
					FeedsTable[keys[i]].refreshrateSpecified = true;
				}

			}

			get { return  refreshrate; } 

		}

		///<summary>
		///Internal flag used to track whether the XML in the feed list validated against the schema. 
		///</summary>
		private static bool  validationErrorOccured = false; 

		/// <summary>
		/// The schema for the RSS feed list format
		/// </summary>
		private XmlSchema feedsSchema = null; 

		/// <summary>
		/// Boolean flag indicates whether the feeds list was loaded 
		/// successfully during the last call to LoadFeedlist()
		/// </summary>
		public bool FeedsListOK{		
			get { return !validationErrorOccured; }
		}
		
		///<summary>Loads the schema for a feedlist into an XmlSchema object. 
		///<seealso cref="feedsSchema"/></summary>		
		private void LoadFeedlistSchema(){
		
			using(Stream xsdStream = Resource.Manager.GetStream("Resources.feedListSchema.xsd")){
				feedsSchema = XmlSchema.Read(xsdStream, null); 
			}
			
		}


		/// <summary>
		/// Loads the RSS feedlist from the given URL and validates it against the schema. 
		/// </summary>
		/// <param name="feedListUrl">The URL of the feedlist</param>
		/// <param name="veh">The event handler that should be invoked on the client if validation errors occur</param>
		/// <exception cref="XmlException">XmlException thrown if XML is not well-formed</exception>
		public void LoadFeedlist(string feedListUrl, ValidationEventHandler veh){
			LoadFeedlist(AsyncWebRequest.GetSyncResponseStream(feedListUrl, null, this.UserAgent, this.Proxy), veh);
			this.SearchHandler.CheckIndex();
		}

		/// <summary>
		/// Loads the RSS feedlist from the given URL and validates it against the schema. 
		/// </summary>
		/// <param name="xmlStream">The XML Stream of a feedlist to load</param>
		/// <param name="veh">The event handler that should be invoked on the client if validation errors occur</param>
		/// <exception cref="XmlException">XmlException thrown if XML is not well-formed</exception>
		public void LoadFeedlist(Stream xmlStream, ValidationEventHandler veh){
			
			XmlParserContext context = new XmlParserContext(null, new RssBanditXmlNamespaceResolver(), null, XmlSpace.None);
			XmlValidatingReader vr = new RssBanditXmlValidatingReader(xmlStream, XmlNodeType.Document, context);
			vr.Schemas.Add(feedsSchema); 
			vr.ValidationType = ValidationType.Schema;	  

			//specify validation event handler passed by caller and the one we use 
			//internally to track state 
			vr.ValidationEventHandler += veh;
			vr.ValidationEventHandler += new ValidationEventHandler(ValidationCallbackOne);
			validationErrorOccured = false; 

			//convert XML to objects
			XmlSerializer serializer = XmlHelper.SerializerCache.GetSerializer(typeof(NewsComponents.Feed.feeds));
			feeds myFeeds = (NewsComponents.Feed.feeds)serializer.Deserialize(vr); 
			vr.Close(); 				
				

			if(!validationErrorOccured){

				//copy feeds over if we are importing a new feed  
				
				if(myFeeds.feed != null){
					foreach(feedsFeed f in myFeeds.feed){						
						if(_feedsTable.Contains(f.link) == false){

							bool isBadUri = false; 
							try {
								Uri uri = new Uri(f.link);
								// CLR 2.0 Uri does not like "news:" scheme, so we 
								// switch it to "nntp:" (see http://msdn2.microsoft.com/en-us/library/system.uri.scheme.aspx)
								if (NntpWebRequest.NewsUriScheme.Equals(uri.Scheme)) {
									f.link = NntpWebRequest.NntpUriScheme + uri.AbsoluteUri.Substring(uri.Scheme.Length);
								}
							}catch(Exception) { isBadUri = true;}

							if(isBadUri){
								continue;
							}else{
								// test again: we may have changed to Uri above:
								if (_feedsTable.Contains(f.link) == false)
									_feedsTable.Add(f.link, f); 							 
							}
						}						
					}
				}

				//copy over category info if we are importing a new feed
				if(myFeeds.categories != null){
					foreach(category cat in myFeeds.categories){
						string cat_trimmed = cat.Value.Trim();
						if(!this.categories.ContainsKey(cat_trimmed)){
							cat.Value = cat_trimmed;
							this.categories.Add(cat_trimmed, cat); 
						}
					}
				}		
		
				//This happens if for some reason the category of a feed didn't end up 
				//in the categories collection during the last save of the feedlist. 
				if(categoryMismatch && (myFeeds.feed != null)){									

					foreach(feedsFeed f in myFeeds.feed){	
						if(f.category != null){								
							string cat_trimmed = f.category = f.category.Trim();								
								
							if(!this.categories.ContainsKey(cat_trimmed)){									
								this.categories.Add(cat_trimmed); 
							}
						}
					}					
					
					categoryMismatch = false; 
				}

				//copy over layout info if we are importing a new feed
				if(myFeeds.listviewLayouts != null){
					foreach(listviewLayout layout in myFeeds.listviewLayouts){
						string layout_trimmed = layout.ID.Trim();
						if(!this.layouts.ContainsKey(layout_trimmed)){
							this.layouts.Add(layout_trimmed,  layout.FeedColumnLayout); 
						}
					}
				}

				//copy nntp-server defs. over if we are importing  
				if(myFeeds.nntpservers != null){
					foreach(NntpServerDefinition sd in myFeeds.nntpservers){						
						if(nntpServers.Contains(sd.Name) == false){
							nntpServers.Add(sd.Name, sd); 							 
						}						
					}
				}
				
				//copy user-identities over if we are importing  
				if(myFeeds.identities != null){
					foreach(UserIdentity ui in myFeeds.identities){						
						if(identities.Contains(ui.Name) == false){
							identities.Add(ui.Name, ui); 							 
						}						
					}
				}

				//if refresh rate in imported feed then use that
				if( myFeeds.refreshrateSpecified){
					this.refreshrate = myFeeds.refreshrate; 					
				}

				//if stylesheet specified in imported feed then use that
				if(!StringHelper.EmptyOrNull(myFeeds.stylesheet)){
					this.stylesheet = myFeeds.stylesheet; 					
				}

				//if download enclosures specified in imported feed then use that
				if(myFeeds.downloadenclosuresSpecified){
					this.downloadenclosures = myFeeds.downloadenclosures; 					
				}

				//if maximum enclosure cache size specified in imported feed then use that
				if(myFeeds.enclosurecachesizeSpecified){
					this.enclosurecachesize = myFeeds.enclosurecachesize; 					
				}

				//if maximum number of enclosures to download on a new feed specified in imported feed then use that
				if(myFeeds.numtodownloadonnewfeedSpecified){
					this.numtodownloadonnewfeed = myFeeds.numtodownloadonnewfeed; 					
				}

				//if cause alert on enclosures specified in imported feed then use that
				if(myFeeds.enclosurealertSpecified){
					this.enclosurealert = myFeeds.enclosurealert; 					
				}

				//if create subfolders for enclosures specified in imported feed then use that
				if(myFeeds.createsubfoldersforenclosuresSpecified){
					this.createsubfoldersforenclosures = myFeeds.createsubfoldersforenclosures; 					
				}


				//if marking items as read on exit specified in imported feed then use that
				if(myFeeds.markitemsreadonexitSpecified){
					this.markitemsreadonexit = myFeeds.markitemsreadonexit; 					
				}

				//if enclosure folder specified in imported feed then use that
				if(!StringHelper.EmptyOrNull(myFeeds.enclosurefolder)){
					this.EnclosureFolder = myFeeds.enclosurefolder; 					
				}

				//if podcast folder specified in imported feed then use that
				if(!StringHelper.EmptyOrNull(myFeeds.podcastfolder)){
					this.PodcastFolder = myFeeds.podcastfolder; 					
				}

				//if podcast file extensions specified in imported feed then use that
				if(!StringHelper.EmptyOrNull(myFeeds.podcastfileexts)){
					this.PodcastFileExtensionsAsString = myFeeds.podcastfileexts; 					
				}
				

				//if listview layout specified in imported feed then use that
				if(!StringHelper.EmptyOrNull(myFeeds.listviewlayout)){
					this.listviewlayout = myFeeds.listviewlayout;
				}

				//if max item age in imported feed then use that
				try{

					if(!StringHelper.EmptyOrNull(myFeeds.maxitemage)){
						this.maxitemage = XmlConvert.ToTimeSpan(myFeeds.maxitemage); 					
					}

				}catch(FormatException fe){
					Trace("Error occured while parsing maximum item age from feed list: {0}" , fe.ToString()); 	
				}

			}
		}
		    


		/// <summary>
		/// Specifies that a feed should be ignored when RefreshFeeds() is called by 
		/// setting its refresh rate to zero. The feed can still be refreshed manually by 
		/// calling GetItemsForFeed(). 
		/// </summary>
		/// <remarks>If no feed with that URL exists then nothing is done.</remarks>
		/// <param name="feedUrl">The URL of the feed to ignore. </param>
		public void DisableFeed(string feedUrl){

			if(!FeedsTable.ContainsKey(feedUrl)){
				return; 
			}
		
			feedsFeed f = FeedsTable[feedUrl];
			f.refreshrate = 0; 
			f.refreshrateSpecified = true; 
		
		}


		/// <summary>
		/// Removes all information related to a feed from the NewsHandler. 
		/// </summary>
		/// <remarks>If the item doesn't exist in the NewsHandler then nothing is done</remarks>
		/// <param name="item">the item to delete</param>
		public void DeleteItem(NewsItem item){

			if(item.Feed != null && !StringHelper.EmptyOrNull( item.Feed.link )){
				
				/* 
				 * There is no attempt to load feed from disk because it is 
				 * assumed that for this to be called the feed was already loaded
				 * since we have an item from the feed */
				
				FeedInfo fi = itemsTable[item.Feed.link] as FeedInfo;
				
				if(fi != null){
					lock(fi.itemsList){
						item.Feed.deletedstories.Add(item.Id); 				
						fi.itemsList.Remove(item); 
					}
				}//if(fi != null)
			}//if(item.Feed != null) 
		
		}

		/// <summary>
		/// Deletes all the items in a feed
		/// </summary>
		/// <param name="feed">the feed</param>
		public void DeleteAllItemsInFeed(feedsFeed feed){


			if (feed != null && !StringHelper.EmptyOrNull( feed.link ) && FeedsTable.ContainsKey(feed.link)) {
			  
				FeedInfo fi = itemsTable[feed.link] as FeedInfo; 

				//load feed from disk 
				if(fi == null){
					fi = (FeedInfo) this.GetFeed(feed); 
				}

				if(fi != null){
					lock(fi.itemsList){
						foreach(NewsItem item in fi.itemsList){
							feed.deletedstories.Add(item.Id); 
						}
						fi.itemsList.Clear(); 
					}					
				}//if(fi != null)		
			
				this.SearchHandler.IndexRemove(feed.id);

			}//if (feed != null && !StringHelper.EmptyOrNull( feed.link ) && FeedsTable.ContainsKey(feed.link)) {
					
		}

		/// <summary>
		/// Deletes all items in a feed
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		public void DeleteAllItemsInFeed(string feedUrl){

			if(FeedsTable.ContainsKey(feedUrl)){
				this.DeleteAllItemsInFeed(FeedsTable[feedUrl]);			
			}
		
		}

		/// <summary>
		/// Undeletes a deleted item
		/// </summary>
		/// <remarks>if the parent feed has been deleted then this does nothing</remarks>
		/// <param name="item">the utem to restore</param>
		public void RestoreDeletedItem(NewsItem item){
		
			if(item.Feed != null && !StringHelper.EmptyOrNull( item.Feed.link ) && FeedsTable.ContainsKey(item.Feed.link)){
				
				FeedInfo fi = itemsTable[item.Feed.link] as FeedInfo;

				//load feed from disk 
				if(fi == null){
					fi = (FeedInfo) this.GetFeed(item.Feed); 
				}
				
				if(fi != null){
					lock(fi.itemsList){
						item.Feed.deletedstories.Remove(item.Id); 				
						fi.itemsList.Add(item); 
					}
				}//if(fi != null)

				this.SearchHandler.IndexAdd(item);
			}//if(item.Feed != null) 
		}

		/// <summary>
		/// Undeletes all the deleted items in the list
		/// </summary>
		/// <remarks>if the parent feed has been deleted then this does nothing</remarks>
		/// <param name="deletedItems">the list of items to restore</param>
		public void RestoreDeletedItem(ArrayList deletedItems){

			foreach(NewsItem item in deletedItems){
				this.RestoreDeletedItem(item); 
			}

			this.SearchHandler.IndexAdd(deletedItems);

		}

		/// <summary>
		/// Removes all information related to a feed from the NewsHandler.   
		/// </summary>
		/// <remarks>If no feed with that URL exists then nothing is done.</remarks>
		/// <param name="feedUrl">The URL of the feed to delete. </param>
		/// <exception cref="ApplicationException">If an error occured while 
		/// attempting to delete the cached feed. Examine the InnerException property 
		/// for details</exception>
		public void DeleteFeed(string feedUrl){

			if(!FeedsTable.ContainsKey(feedUrl)){
				return; 
			}
		
			feedsFeed f = FeedsTable[feedUrl];
			FeedsTable.Remove(feedUrl); 

			if(itemsTable.Contains(feedUrl)){
				itemsTable.Remove(feedUrl); 
			}

			this.SearchHandler.IndexRemove(f.id);
			this.enclosureDownloader.CancelPendingDownloads(feedUrl); 

			try{ 
				this.CacheHandler.RemoveFeed(f); 
				
			}catch(Exception e){
				throw new ApplicationException(e.Message, e); 
			}
		
		}

		/// <summary>
		/// Saves the feed list to the specified stream. The feed is written in 
		/// the RSS Bandit feed file format as described in feeds.xsd
		/// </summary>
		/// <param name="feedStream">The stream to save the feed list to</param>
		public void SaveFeedList(Stream feedStream){
			this.SaveFeedList(feedStream, FeedListFormat.NewsHandler); 
		}


		/// <summary>
		/// Helper method used for constructing OPML file. It traverses down the tree on the 
		/// path defined by 'category' starting with 'startNode'. 
		/// </summary>
		/// <param name="startNode">Node to start with</param>
		/// <param name="category">A category path, e.g. 'Category1\SubCategory1'.</param>
		/// <returns>The leaf category node.</returns>
		/// <remarks>If one category in the path is not found, it will be created.</remarks>
		private XmlElement CreateCategoryHive(XmlElement startNode, string category)	{

			

			if (category == null || category.Length == 0 || startNode == null) return startNode;

			string[] catHives = category.Split(NewsHandler.CategorySeparator.ToCharArray());
			XmlElement n = null;
			bool wasNew = false;

			foreach (string catHive in catHives){

				if (!wasNew){ 
					string xpath = "child::outline[@title=" + buildXPathString(catHive) + " and (count(@*)= 1)]";				 
					n = (XmlElement) startNode.SelectSingleNode(xpath); 
				}else{
					n = null;
				}

				if (n == null) {
					
				 
					n = startNode.OwnerDocument.CreateElement("outline"); 
					n.SetAttribute("title", catHive); 
					startNode.AppendChild(n);
					wasNew = true;	// shorten search
				}

				startNode = n;

			}//foreach
			
			return startNode;
		}


		/// <summary>
		/// Helper function that gets the listview layout with the specified ID from the
		/// Arraylist
		/// </summary>
		/// <param name="id"></param>
		/// <param name="layouts"></param>
		/// <returns></returns>
		private static listviewLayout FindLayout(string id, ArrayList layouts){
		
			foreach(listviewLayout layout in layouts){
				if(id.Equals(layout.ID))
					return layout; 			
			}
			return null; 
		}

		/// <summary>
		/// Helper function breaks up a string containing quote characters into 
		///	a series of XPath concat() calls. 
		/// </summary>
		/// <param name="input">input string</param>
		/// <returns>broken up string</returns>
		public static string buildXPathString (string input) {
			string[] components = input.Split(new char[] { '\''});
			string result = "";
			result += "concat(''";
			for (int i = 0; i < components.Length; i++) {
				result += ", '" + components[i] + "'";
				if (i < components.Length - 1) {
					result += ", \"'\"";
				}
			}
			result += ")";
			Console.WriteLine(result);
			return result;
		}

		/// <summary>
		/// Saves the whole feed list incl. empty categories to the specified stream
		/// </summary>
		/// <param name="feedStream">The feedStream to save the feed list to</param>
		/// <param name="format">The format to save the stream as. </param>
		/// <exception cref="InvalidOperationException">If anything wrong goes on with XmlSerializer</exception>
		/// <exception cref="ArgumentNullException">If feedStream is null</exception>
		public void SaveFeedList(Stream feedStream, FeedListFormat format){
			this.SaveFeedList(feedStream, format, this._feedsTable, true);
		}

		/// <summary>
		/// Saves the provided feed list to the specified stream
		/// </summary>
		/// <param name="feedStream">The feedStream to save the feed list to</param>
		/// <param name="format">The format to save the stream as. </param>
		/// <param name="feeds">FeedsCollection containing the feeds to save. 
		/// Can contain a subset of the owned feeds collection</param>
		/// <param name="includeEmptyCategories">Set to true, if categories without a contained feed should be included</param>
		/// <exception cref="InvalidOperationException">If anything wrong goes on with XmlSerializer</exception>
		/// <exception cref="ArgumentNullException">If feedStream is null</exception>
		public void SaveFeedList(Stream feedStream, FeedListFormat format, FeedsCollection feeds, bool includeEmptyCategories){

			if (feedStream == null)
				throw new ArgumentNullException("feedStream");

			if(format.Equals(FeedListFormat.OPML)){

				XmlDocument opmlDoc = new XmlDocument(); 
				opmlDoc.LoadXml("<opml version='1.0'><head /><body /></opml>"); 

				Hashtable categoryTable = new Hashtable(categories.Count); 
				//CategoriesCollection categoryList = (CategoriesCollection)categories.Clone();
			
				foreach(feedsFeed f in feeds.Values) {

					XmlElement outline = opmlDoc.CreateElement("outline"); 
					outline.SetAttribute("title", f.title); 
					outline.SetAttribute("xmlUrl", f.link); 
					outline.SetAttribute("type", "rss"); 
					outline.SetAttribute("text", f.title); 
			  
					FeedInfo fi  = (FeedInfo) itemsTable[f.link];
			  
					if(fi != null){
						outline.SetAttribute("htmlUrl", fi.Link); 
						outline.SetAttribute("description", fi.Description); 
					}


					string category = (f.category == null ? String.Empty: f.category);
				
					XmlElement catnode;
					if (categoryTable.ContainsKey(category))
						catnode = (XmlElement)categoryTable[category];
					else {
						catnode = CreateCategoryHive((XmlElement) opmlDoc.DocumentElement.ChildNodes[1], category);
						categoryTable.Add(category, catnode); 
					}

					catnode.AppendChild(outline);			 			
				}

				if (includeEmptyCategories) {
					//add categories, we don't already have
					foreach(string category in this.categories.Keys) {
						CreateCategoryHive((XmlElement) opmlDoc.DocumentElement.ChildNodes[1], category);
					}
				}

				XmlTextWriter opmlWriter = new XmlTextWriter(feedStream,System.Text.Encoding.UTF8); 
				opmlWriter.Formatting    = Formatting.Indented; 
				opmlDoc.Save(opmlWriter); 

			}else if(format.Equals(FeedListFormat.NewsHandler)|| format.Equals(FeedListFormat.NewsHandlerLite)){ 

				XmlSerializer serializer = XmlHelper.SerializerCache.GetSerializer(typeof(NewsComponents.Feed.feeds));
				feeds feedlist = new feeds(); 

				if(feeds != null){
					
					feedlist.refreshrate = this.refreshrate;
					feedlist.refreshrateSpecified = true; 

					feedlist.downloadenclosures = this.downloadenclosures;
					feedlist.downloadenclosuresSpecified = true; 

					feedlist.enclosurealert = this.enclosurealert;
					feedlist.enclosurealertSpecified = true; 

					feedlist.createsubfoldersforenclosures = this.createsubfoldersforenclosures;
					feedlist.createsubfoldersforenclosuresSpecified = true; 

					feedlist.numtodownloadonnewfeed = this.numtodownloadonnewfeed;
					feedlist.numtodownloadonnewfeedSpecified = true; 

					feedlist.enclosurecachesize     = this.enclosurecachesize;
					feedlist.enclosurecachesizeSpecified = true;

					feedlist.maxitemage = XmlConvert.ToString(this.maxitemage);
					feedlist.listviewlayout = this.listviewlayout;
					feedlist.stylesheet = this.stylesheet;
					feedlist.enclosurefolder = this.EnclosureFolder;					
					feedlist.podcastfolder   = this.PodcastFolder;
					feedlist.podcastfileexts = this.PodcastFileExtensionsAsString;
					feedlist.markitemsreadonexit = this.markitemsreadonexit;
					feedlist.markitemsreadonexitSpecified = true; 
				
					foreach(feedsFeed f in feeds.Values){
						feedlist.feed.Add(f); 

						if(itemsTable.Contains(f.link)){
									
							ArrayList items = ((FeedInfo)itemsTable[f.link]).itemsList;
							 
							// Taken out because it meant that when we sync we lose information
							// about stuff we've read from other instances of RSS Bandit synced from 
							// if its cache is older than this one. 
							/* f.storiesrecentlyviewed.Clear(); */
							 

							if(!format.Equals(FeedListFormat.NewsHandlerLite)){
								foreach(NewsItem ri in items){
									if(ri.BeenRead && !f.storiesrecentlyviewed.Contains(ri.Id)){ //THIS MAY BE SLOW
										f.storiesrecentlyviewed.Add(ri.Id); 	 
									}
								}
							}//foreach
						
						}//if
					}//foreach

				}//if(feeds != null) 


				ArrayList c =  new ArrayList(this.categories.Count); 
				/* sometimes we get nulls in the arraylist, remove them */
				for(int i=0; i < this.categories.Count; i++){
					CategoryEntry s = this.categories[i]; 
					if(s.Value.Value == null){
						this.categories.RemoveAt(i); 
						i--;			
					} else {
						c.Add(s.Value);
					}
				}

				//we don't want to write out empty <categories /> into the schema. 				
				if((c== null) || (c.Count == 0)){
					feedlist.categories = null; 
				}else{
					feedlist.categories = c; 
				}
				

				c =  new ArrayList(this.layouts.Count); 
				/* sometimes we get nulls in the arraylist, remove them */
				for(int i=0; i < this.layouts.Count; i++){
					FeedColumnLayoutEntry s = this.layouts[i]; 
					if(s.Value == null){
						this.layouts.RemoveAt(i); 
						i--;			
					} else {
						c.Add(new listviewLayout(s.Key, s.Value));
					}
				}

				//we don't want to write out empty <listview-layouts /> into the schema. 				
				if((c== null) || (c.Count == 0)){
					feedlist.listviewLayouts = null; 
				}else{
					feedlist.listviewLayouts = c; 
				}

				c =  new ArrayList(this.nntpServers.Values); 

				//we don't want to write out empty <nntp-servers /> into the schema. 				
				if((c== null) || (c.Count == 0)){
					feedlist.nntpservers = null; 
				}else{
					feedlist.nntpservers = c; 
				}

				c =  new ArrayList(this.identities.Values); 

				//we don't want to write out empty <user-identities /> into the schema. 				
				if((c== null) || (c.Count == 0)){
					feedlist.identities = null; 
				}else{
					feedlist.identities = c; 
				}


				TextWriter writer = new StreamWriter(feedStream);
				serializer.Serialize(writer, feedlist);
				//writer.Close(); DON'T CLOSE STREAM
								
			}
		}


		/// <summary>
		/// Used to clear the information about when last the feed was downloaded. This allows
		/// us to refetch the feed without sending If-Modified-Since or If-None-Match header
		/// information and thus force a download. 
		/// </summary>
		/// <param name="f">The feed to mark for download</param>
		public void MarkForDownload(feedsFeed f){
			f.etag = null; 
			f.lastretrievedSpecified = false; 
			f.lastretrieved = DateTime.MinValue;		
			f.lastmodified = DateTime.MinValue;
		}
		

		/// <summary>
		/// Used to clear the information about when last the feeds downloaded. This allows
		/// us to refetch the feed without sending If-Modified-Since or If-None-Match header
		/// information and thus force a download. 
		/// </summary>		
		public void MarkForDownload(){
			if(this.FeedsListOK){
				foreach(feedsFeed f in this.FeedsTable.Values){
					this.MarkForDownload(f);
				}
			}
		}

		/// <summary>
		/// Removes all the RSS items cached in-memory and on-disk for all feeds. 
		/// </summary>
		public void ClearItemsCache(){
			this.itemsTable.Clear(); 
			this.CacheHandler.ClearCache(); 
		}		


		/// <summary>
		/// Marks all items stored in the internal cache of RSS items as read.
		/// </summary>
		public void MarkAllCachedItemsAsRead(){
		
			foreach(feedsFeed f in this.FeedsTable.Values) {
				this.MarkAllCachedItemsAsRead(f); 			
			}

		}


		/// <summary>
		/// Marks all items stored in the internal cache of RSS items as read
		/// for a particular category.
		/// </summary>
		/// <param name="category">The category the feeds belong to</param>
		public void MarkAllCachedCategoryItemsAsRead(string category){
		
			if(FeedsListOK){

				if(this.categories.ContainsKey(category)) {

					foreach(feedsFeed f in this.FeedsTable.Values) {
					
						if((f.category!= null) && f.category.Equals(category)) {
							this.MarkAllCachedItemsAsRead(f); 			
						}
					}
				}
				else if (category == null /* the default category */) {
					foreach(feedsFeed f in this.FeedsTable.Values) {
					
						if (f.category== null) {
							this.MarkAllCachedItemsAsRead(f); 			
						}
					}
				}

			}//if(FeedsListOK)
		}
	
		/// <summary>
		/// Marks all items stored in the internal cache of RSS items as read
		/// for a particular feed.
		/// </summary>
		/// <param name="feedUrl">The URL of the RSS feed</param>
		public void MarkAllCachedItemsAsRead(string feedUrl){
		
			if (!StringHelper.EmptyOrNull( feedUrl )) {
			
				feedsFeed feed = this.FeedsTable[feedUrl];
				if (feed != null){
					this.MarkAllCachedItemsAsRead(feed);
				}
			}
		}

		/// <summary>
		/// Marks all items stored in the internal cache of RSS items as read
		/// for a particular feed.
		/// </summary>
		/// <param name="feed">The RSS feed</param>
		public void MarkAllCachedItemsAsRead(feedsFeed feed){
		
			if (feed != null && !StringHelper.EmptyOrNull( feed.link )) {
			  
				FeedInfo fi = itemsTable[feed.link] as FeedInfo; 

				if(fi != null){
					foreach(NewsItem ri in fi.itemsList){
						ri.BeenRead = true; 
					}
				}

				feed.containsNewMessages = false;
			}
		}


		
		/// <summary>
		/// Adds a feed and associated FeedInfo object to the FeedsTable and itemsTable. 
		/// Any existing feed objects are replaced by the new objects. 
		/// </summary>
		/// <param name="f">The feedsFeed object </param>
		/// <param name="fi">The FeedInfo object</param>
		public void AddFeed(feedsFeed f, FeedInfo fi){
		
			if(f != null){
				lock(this.FeedsTable){
					if(FeedsTable.ContainsKey(f.link)){
						FeedsTable.Remove(f.link); 
					}
					FeedsTable.Add(f.link, f); 				
				}
			}

			if(fi != null){
				lock(this.itemsTable) {
					if(itemsTable.ContainsKey(f.link)){
						itemsTable.Remove(f.link); 
					}
					itemsTable.Add(f.link, fi); 
				}
			}

		}

		/// <summary>
		/// Defines all cache relevant feedsFeed properties, 
		/// that requires we have to (re-)write the cached file. 
		/// </summary>
		private const NewsFeedProperty cacheRelevantPropertyChanges =
			NewsFeedProperty.FeedItemFlag |
			NewsFeedProperty.FeedItemReadState |
			NewsFeedProperty.FeedItemCommentCount |
			NewsFeedProperty.FeedItemNewCommentsRead |
			NewsFeedProperty.FeedItemWatchComments |
			NewsFeedProperty.FeedCredentials;

		/// <summary>
		/// Determines whether the changed specified properties 
		/// are cache relevant changes (feed cache file have to be (re-)written.
		/// </summary>
		/// <param name="changedProperty">The changed property or properties.</param>
		/// <returns>
		/// 	<c>true</c> if it is a cache relevant change; otherwise, <c>false</c>.
		/// </returns>
		public bool IsCacheRelevantChange(NewsFeedProperty changedProperty) {
			return (cacheRelevantPropertyChanges & changedProperty) != NewsFeedProperty.None;
		}

		/// <summary>
		/// Defines all subscription relevant feedsFeed properties, 
		/// that requires we have to (re-)write the subscription file. 
		/// </summary>
		private const NewsFeedProperty subscriptionRelevantPropertyChanges =
			NewsFeedProperty.FeedLink |
			NewsFeedProperty.FeedTitle |
			NewsFeedProperty.FeedCategory |
			NewsFeedProperty.FeedItemsDeleteUndelete |
			NewsFeedProperty.FeedItemReadState |
			NewsFeedProperty.FeedMaxItemAge |
			NewsFeedProperty.FeedRefreshRate |
			NewsFeedProperty.FeedCacheUrl |
			NewsFeedProperty.FeedAdded |
			NewsFeedProperty.FeedRemoved |
			NewsFeedProperty.FeedCategoryAdded |
			NewsFeedProperty.FeedCategoryRemoved |
			NewsFeedProperty.FeedAlertOnNewItemsReceived |
			NewsFeedProperty.FeedMarkItemsReadOnExit |
			NewsFeedProperty.General;

		/// <summary>
		/// Determines whether the changed specified properties 
		/// are subscription relevant changes (subscription file have to be (re-)written.
		/// </summary>
		/// <param name="changedProperty">The changed property or properties.</param>
		/// <returns>
		/// 	<c>true</c> if it is a subscription relevant change; otherwise, <c>false</c>.
		/// </returns>
		public bool IsSubscriptionRelevantChange(NewsFeedProperty changedProperty) {
			return (subscriptionRelevantPropertyChanges & changedProperty) != NewsFeedProperty.None;
		}

		/// <summary>
		/// Do apply any internal work needed after some feed or feed item properties 
		/// or content was changed outside.
		/// </summary>
		/// <param name="feedUrl">The feed to update</param>
		/// <exception cref="ArgumentNullException">If feedUrl is null or empty</exception>
		public void ApplyFeedModifications(string feedUrl) {
		  
			if (feedUrl == null || feedUrl.Length == 0)
				throw new ArgumentNullException("feedUrl");

			FeedDetailsInternal fi = null;
			feedsFeed f = null;
			if(itemsTable.Contains(feedUrl)){
				fi = (FeedDetailsInternal) itemsTable[feedUrl]; 			
			}
			if(this.FeedsTable.Contains(feedUrl)){
				f = this.FeedsTable[feedUrl]; 
			}
			if (fi != null && f != null) {
				try {
					f.cacheurl = this.SaveFeed(f);
				} catch (Exception ex){
					Trace("ApplyFeedModifications() cause exception while saving feed '{0}'to cache: {1}", feedUrl, ex.Message);
				}
			}

		}


		/// <summary>
		/// Tests whether a particular propery value is set
		/// </summary>
		/// <param name="value">the value to test</param>
		/// <param name="propertyName">Name of the property to set</param>
		/// <param name="owner">the object which the property comes from</param>
		/// <returns>true if it is set and false otherwise</returns>
		private static bool IsPropertyValueSet(object value, string propertyName, object owner){
		
			//TODO: Make this code more efficient

			if(value == null){
			
				return false; 
			
			}else if(value is string){		
	
				bool isSet = !StringHelper.EmptyOrNull((string) value); 

				if(propertyName.Equals("maxitemage") && isSet){
					isSet = !value.Equals(XmlConvert.ToString(TimeSpan.MaxValue));
				}

				return isSet; 
			}else{
			
				return (bool) owner.GetType().GetField(propertyName + "Specified").GetValue(owner);  
			}
		}
		

		/// <summary>
		/// Gets the value of a feed's property. This does not inherit the properties of parent
		/// categories. 
		/// </summary>
		/// <param name="feedUrl">the feed URL</param>
		/// <param name="propertyName">the name of the property</param>		
		/// <returns>the value of the property</returns>
		private object GetFeedProperty(string feedUrl, string propertyName){
		
			return this.GetFeedProperty(feedUrl, propertyName, false);
		}

		/// <summary>
		/// Gets the value of a feed's property
		/// </summary>
		/// <param name="feedUrl">the feed URL</param>
		/// <param name="propertyName">the name of the property</param>
		/// <param name="inheritCategory">indicates whether the settings from the parent category should be inherited or not</param>
		/// <returns>the value of the property</returns>
		private object GetFeedProperty(string feedUrl, string propertyName, bool inheritCategory){
		
			//TODO: Make this code more efficient

			object value = this.GetType().GetField(propertyName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this); 			

			if(_feedsTable.ContainsKey(feedUrl)){

				feedsFeed f = this.FeedsTable[feedUrl]; 
				object f_value = f.GetType().GetField(propertyName).GetValue(f);

				if(IsPropertyValueSet(f_value, propertyName, f)){			
				
					if(propertyName.Equals("maxitemage")){
						f_value = XmlConvert.ToTimeSpan((string)f_value);
					}
												   
					value = f_value; 

				}else if(inheritCategory && !StringHelper.EmptyOrNull(f.category)){
				
					category c = this.Categories.GetByKey(f.category);
					
					while(c != null){

						object c_value = c.GetType().GetField(propertyName).GetValue(c);
					
						if(IsPropertyValueSet(c_value, propertyName, c)){	

							if(propertyName.Equals("maxitemage")){
								c_value = XmlConvert.ToTimeSpan((string)c_value);
							}												   
							value = c_value; 							
							break; 
						}else{
							c = c.parent; 
						}
					}//while
				}//else if(!StringHelper.EmptyOrNull(f.category))

			}//if(_feedsTable.ContainsKey(feedUrl)){

			
			return value; 
		}

		/// <summary>
		/// Sets the value of a feed property.
		/// </summary>
		/// <param name="feedUrl"></param>
		/// <param name="propertyName"></param>
		/// <param name="value"></param>
		private void SetFeedProperty(string feedUrl, string propertyName, object value){

			//TODO: Make this code more efficient

			if(_feedsTable.ContainsKey(feedUrl)){
				feedsFeed f = this.FeedsTable[feedUrl]; 
				
				if(value is TimeSpan){
					value = XmlConvert.ToString((TimeSpan)value);
				}				
				f.GetType().GetField(propertyName).SetValue(f, value);
 				
				if((value != null) && !(value is string)){
					f.GetType().GetField(propertyName + "Specified").SetValue(f, true); 
				}
			}	
		
		}
		
		/// <summary>
		///  Sets the maximum amount of time an item should be kept in the 
		/// cache for a particular feed. This overrides the value of the 
		/// maxItemAge property. 
		/// </summary>
		/// <remarks>If the feed URL is not found in the FeedsTable then nothing happens</remarks>
		/// <param name="feedUrl">The feed</param>
		/// <param name="age">The maximum amount of time items should be kept for the 
		/// specified feed.</param>
		public  void SetMaxItemAge(string feedUrl, TimeSpan age){
			
			this.SetFeedProperty(feedUrl, "maxitemage", age); 								
		} 

		/// <summary>
		/// Gets the maximum amount of time an item is kept in the 
		/// cache for a particular feed. 
		/// </summary>
		/// <param name="feedUrl">The feed identifier</param>
		/// <exception cref="FormatException">if an error occurs while converting the max item age value to a TimeSpan</exception>
		public TimeSpan GetMaxItemAge(string feedUrl){			
			
			return (TimeSpan) this.GetFeedProperty(feedUrl, "maxitemage", true); 		
		} 
		

		/// <summary>
		/// Sets the refresh rate for a feed
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <param name="refreshRate">the new refresh rate</param>
		public void SetRefreshRate(string feedUrl, int refreshRate){
		
			this.SetFeedProperty(feedUrl, "refreshrate", refreshRate); 
		}

		/// <summary>
		/// Gets the refresh rate for a feed
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <returns>the refresh rate</returns>
		public int GetRefreshRate(string feedUrl){
		
			return (int) this.GetFeedProperty(feedUrl, "refreshrate", true); 
		}

		/// <summary>
		/// Sets the stylesheet for a feed
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <param name="stylesheet">the new stylesheet</param>
		public void SetStyleSheet(string feedUrl, string stylesheet){
		
			this.SetFeedProperty(feedUrl, "stylesheet", stylesheet); 
		}

		/// <summary>
		/// Gets the stylesheet for a feed
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <returns>the stylesheet</returns>
		public string GetStyleSheet(string feedUrl){
		
			return (string) this.GetFeedProperty(feedUrl, "stylesheet"); 
		}


		/// <summary>
		/// Sets the enclosure folder for a feed
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <param name="enclosurefolder">the new enclosure folder </param>
		public void SetEnclosureFolder(string feedUrl, string enclosurefolder){
		
			this.SetFeedProperty(feedUrl, "enclosurefolder", enclosurefolder); 
		}

		/// <summary>
		/// Gets the target folder to download enclosures from a feed. The folder returned 
		/// may change depending on whether the item is a podcast (i.e. is in the 
		/// podcastfileextensions ArrayList)
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <param name="filename">The name of the file</param>
		/// <returns>the enclosure folder</returns>
		public string GetEnclosureFolder(string feedUrl, string filename){
		
			string folderName = ( IsPodcast(filename) ? this.PodcastFolder : this.EnclosureFolder );
			
			if(this.CreateSubfoldersForEnclosures && this.FeedsTable.Contains(feedUrl)){
				feedsFeed f = FeedsTable[feedUrl];
				folderName = Path.Combine(folderName, FileHelper.CreateValidFileName(f.title));				
			}

			return folderName; 
		}


		/// <summary>
		/// Sets the listview layout for a feed
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <param name="listviewlayout">the new listview layout </param>
		public void SetFeedColumnLayout(string feedUrl, string listviewlayout){
		
			this.SetFeedProperty(feedUrl, "listviewlayout", listviewlayout); 
		}

		/// <summary>
		/// Gets the listview layout for a feed
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <returns>the listview layout</returns>
		public string GetFeedColumnLayout(string feedUrl){
		
			return (string) this.GetFeedProperty(feedUrl, "listviewlayout"); 
		}


		/// <summary>
		/// Sets whether to mark items as read on exiting the feed in the UI
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <param name="markitemsreadonexit">the new value for markitemsreadonexit</param>
		public void SetMarkItemsReadOnExit(string feedUrl, bool markitemsreadonexit){
		
			this.SetFeedProperty(feedUrl, "markitemsreadonexit", markitemsreadonexit); 
		}

		/// <summary>
		/// Gets whether to mark items as read on exiting the feed in the UI
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <returns>whether to mark items as read on exit</returns>
		public bool GetMarkItemsReadOnExit(string feedUrl){
		
			return (bool) this.GetFeedProperty(feedUrl, "markitemsreadonexit"); 
		}

		/// <summary>
		/// Sets whether to download enclosures for this feed
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <param name="downloadenclosures">the new value for downloadenclosures</param>
		public void SetDownloadEnclosures(string feedUrl, bool downloadenclosures){
		
			this.SetFeedProperty(feedUrl, "downloadenclosures", downloadenclosures); 
		}

		/// <summary>
		/// Gets whether to download enclosures for this feed
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <returns>hether to download enclosures for this feed</returns>
		public bool GetDownloadEnclosures(string feedUrl){
		
			return (bool) this.GetFeedProperty(feedUrl, "downloadenclosures"); 
		}


		/// <summary>
		/// Sets whether to display an alert when an enclosure is successfully
		/// downloaded for this feed
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <param name="enclosurealert">if set to <c>true</c> [enclosurealert].</param>
		public void SetEnclosureAlert(string feedUrl, bool enclosurealert){
		
			this.SetFeedProperty(feedUrl, "enclosurealert", enclosurealert); 
		}

		/// <summary>
		/// Gets whether to display an alert when an enclosure is successfully 
		/// downloaded for this feed
		/// </summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <returns>hether to download enclosures for this feed</returns>
		public bool GetEnclosureAlert(string feedUrl){
		
			return (bool) this.GetFeedProperty(feedUrl, "enclosurealert"); 
		}

		/// <summary>
		/// Gets the value of a category's property
		/// </summary>
		/// <param name="category">the category name</param>
		/// <param name="propertyName">the name of the property</param>
		/// <returns>the value of the property</returns>
		private object GetCategoryProperty(string category, string propertyName){
		
			//TODO: Make this code more efficient

			object value = this.GetType().GetField(propertyName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this); 			

			if(!StringHelper.EmptyOrNull(category)){
				
				category c = this.Categories.GetByKey(category);
					
				while(c != null){

					object c_value = c.GetType().GetField(propertyName).GetValue(c);
					
					if(IsPropertyValueSet(c_value, propertyName, c)){	

						if(propertyName.Equals("maxitemage")){
							c_value = XmlConvert.ToTimeSpan((string)c_value);
						}												   
						value = c_value; 							
						break; 
					}else{
						c = c.parent; 
					}
				}//while
			}//if(!StringHelper.EmptyOrNull(category))

			

			
			return value; 
		}

		/// <summary>
		/// Sets the value of a category's property.
		/// </summary>
		/// <param name="category">the category's name</param>
		/// <param name="propertyName">the name of the property</param>
		/// <param name="value">the new value</param>
		private void SetCategoryProperty(string category, string propertyName, object value){

			//TODO: Make this code more efficient

			if(!StringHelper.EmptyOrNull(category)){

				//category c = this.Categories.GetByKey(category);
				
				foreach(category c in this.Categories.Values){
									
					//if(c!= null){			

					if(c.Value.Equals(category) || c.Value.StartsWith(category + NewsHandler.CategorySeparator)){
				
						if(value is TimeSpan){
							value = XmlConvert.ToString((TimeSpan)value);
						}
			
						c.GetType().GetField(propertyName).SetValue(c, value);
 				
						if((value != null) && !(value is string)){
							c.GetType().GetField(propertyName + "Specified").SetValue(c, true); 
						}

						break;
					}//if(c!= null) 
				}//foreach
		
			}//	if(!StringHelper.EmptyOrNull(category)){
		}


		/// <summary>
		///  Sets the maximum amount of time an item should be kept in the 
		/// cache for a particular category. This overrides the value of the 
		/// maxItemAge property. 
		/// </summary>
		/// <remarks>If the feed URL is not found in the FeedsTable then nothing happens</remarks>
		/// <param name="category">The feed</param>
		/// <param name="age">The maximum amount of time items should be kept for the 
		/// specified feed.</param>
		public  void SetCategoryMaxItemAge(string category, TimeSpan age){
			
			this.SetCategoryProperty(category, "maxitemage", age); 								
		} 

		/// <summary>
		/// Gets the maximum amount of time an item is kept in the 
		/// cache for a particular feed. 
		/// </summary>
		/// <param name="category">The name of the category</param>
		/// <exception cref="FormatException">if an error occurs while converting the max item age value to a TimeSpan</exception>
		public TimeSpan GetCategoryMaxItemAge(string category){			
			
			return (TimeSpan) this.GetCategoryProperty(category, "maxitemage"); 		
		} 
		

		/// <summary>
		/// Sets the refresh rate for a category
		/// </summary>
		/// <param name="category">the name of the category</param>
		/// <param name="refreshRate">the new refresh rate</param>
		public void SetCategoryRefreshRate(string category, int refreshRate){
		
			this.SetCategoryProperty(category, "refreshrate", refreshRate); 
		}

		/// <summary>
		/// Gets the refresh rate for a category
		/// </summary>
		/// <param name="category">the name of the category</param>
		/// <returns>the refresh rate</returns>
		public int GetCategoryRefreshRate(string category){
		
			return (int) this.GetCategoryProperty(category, "refreshrate"); 
		}

		/// <summary>
		/// Sets the stylesheet for a category
		/// </summary>
		/// <param name="category">the name of the category</param>
		/// <param name="stylesheet">the new stylesheet</param>
		public void SetCategoryStyleSheet(string category, string stylesheet){
		
			this.SetCategoryProperty(category, "stylesheet", stylesheet); 
		}

		/// <summary>
		/// Gets the stylesheet for a category
		/// </summary>
		/// <param name="category">the name of the category</param>
		/// <returns>the stylesheet</returns>
		public string GetCategoryStyleSheet(string category){
		
			return (string) this.GetCategoryProperty(category, "stylesheet"); 
		}


		/// <summary>
		/// Sets the enclosure folder for a category
		/// </summary>
		/// <param name="category">the name of the category</param>
		/// <param name="enclosurefolder">the new enclosure folder </param>
		public void SetCategoryEnclosureFolder(string category, string enclosurefolder){
		
			this.SetCategoryProperty(category, "enclosurefolder", enclosurefolder); 
		}

		/// <summary>
		/// Gets the enclosure folder for a category
		/// </summary>
		/// <param name="category">the name of the category</param>
		/// <returns>the enclosure folder</returns>
		public string GetCategoryEnclosureFolder(string category){
		
			return (string) this.GetCategoryProperty(category, "enclosurefolder"); 
		}


		/// <summary>
		/// Sets the listview layout for a category
		/// </summary>
		/// <param name="category">the name of the category</param>
		/// <param name="listviewlayout">the new listview layout </param>
		public void SetCategoryFeedColumnLayout(string category, string listviewlayout){
		
			this.SetCategoryProperty(category, "listviewlayout", listviewlayout); 
		}

		/// <summary>
		/// Gets the listview layout for a category
		/// </summary>
		/// <param name="category">the name of the category</param>
		/// <returns>the listview layout</returns>
		public string GetCategoryFeedColumnLayout(string category){
		
			return (string) this.GetCategoryProperty(category, "listviewlayout"); 
		}


		/// <summary>
		/// Sets whether to mark items as read on exiting the feed in the UI
		/// </summary>
		/// <param name="category">the name of the category</param>
		/// <param name="markitemsreadonexit">the new value for markitemsreadonexit</param>
		public void SetCategoryMarkItemsReadOnExit(string category, bool markitemsreadonexit){
		
			this.SetCategoryProperty(category, "markitemsreadonexit", markitemsreadonexit); 
		}

		/// <summary>
		/// Gets whether to mark items as read on exiting the feed in the UI
		/// </summary>
		/// <param name="category">the name of the category</param>
		/// <returns>whether to mark items as read on exit</returns>
		public bool GetCategoryMarkItemsReadOnExit(string category){
		
			return (bool) this.GetCategoryProperty(category, "markitemsreadonexit"); 
		}

		/// <summary>
		/// Sets whether to download enclosures for this category
		/// </summary>
		/// <param name="category">the name of the category</param>
		/// <param name="downloadenclosures">the new value for downloadenclosures</param>
		public void SetCategoryDownloadEnclosures(string category, bool downloadenclosures){
		
			this.SetCategoryProperty(category, "downloadenclosures", downloadenclosures); 
		}

		/// <summary>
		/// Gets whether to download enclosures for this category
		/// </summary>
		/// <param name="category">the name of the category</param>
		/// <returns>the refresh rate</returns>
		public bool GetCategoryDownloadEnclosures(string category){
		
			return (bool) this.GetCategoryProperty(category, "downloadenclosures"); 
		}



		/// <summary>
		/// Sets whether to display an alert when an enclosure is successfully downloaded
		/// </summary>
		/// <param name="category">the name of the category</param>
		/// <param name="enclosurealert">if set to <c>true</c> [enclosurealert].</param>
		public void SetCategoryEnclosureAlert(string category, bool enclosurealert){
		
			this.SetCategoryProperty(category, "enclosurealert", enclosurealert); 
		}

		/// <summary>
		/// Gets whether to display an alert when an enclosure is successfully downloaded
		/// </summary>
		/// <param name="category">the name of the category</param>
		/// <returns>the refresh rate</returns>
		public bool GetCategoryEnclosureAlert(string category){
		
			return (bool) this.GetCategoryProperty(category, "enclosurealert"); 
		}

		/// <summary>
		/// Returns the FeedDetails of a feed.
		/// </summary>
		/// <param name="feedUrl">string feed's Url</param>
		/// <returns>FeedInfo or null, if feed was removed or parameter is invalid</returns>
		public IFeedDetails GetFeedInfo(string feedUrl) {
			return this.GetFeedInfo(feedUrl, null);
		}

		/// <summary>
		/// Returns the FeedDetails of a feed.
		/// </summary>
		/// <param name="feedUrl">string feed's Url</param>
		/// <param name="credentials">ICredentials, optional. Can be null</param>
		/// <returns>FeedInfo or null, if feed was removed or parameter is invalid</returns>
		public IFeedDetails GetFeedInfo(string feedUrl, ICredentials credentials) {
			
			if (StringHelper.EmptyOrNull(feedUrl))
				return null;

			IFeedDetails fd = null;

			if(!itemsTable.ContainsKey(feedUrl)){
				feedsFeed theFeed = FeedsTable[feedUrl];
			  
				if (theFeed == null) {//external feed?

					using (Stream mem = AsyncWebRequest.GetSyncResponseStream(feedUrl, credentials, this.UserAgent, this.Proxy)) {
						feedsFeed f = new feedsFeed();
						f.link = feedUrl;
						if (RssParser.CanProcessUrl(feedUrl)) {
							fd = RssParser.GetItemsForFeed(f, mem, false); 
						}
						//TODO: NntpHandler.CanProcessUrl()
					}
					return fd;
				}

				fd = this.GetFeed(theFeed); 					 
				lock(itemsTable){	
					//if feed was in cache but not in itemsTable we load it into itemsTable
					if(!itemsTable.ContainsKey(feedUrl) && (fd!= null)){
						itemsTable.Add(feedUrl, fd); 
					}
				}
			} else {
				fd = (IFeedDetails)itemsTable[feedUrl];
			}

			return fd;
		}
		
	  

		/// <summary>
		/// Reads the RSS feed from the feedsFeed link then caches and returns the feed items 
		/// in an array list.
		/// </summary>
		/// <param name="f">Information about the feed. This information is updated based
		/// on the results of processing the feed. </param>
		/// <returns>An arraylist of News items (i.e. instances of the NewsItem class)</returns>
		/// <exception cref="ApplicationException">If the RSS feed is not 
		/// version 0.91, 1.0 or 2.0</exception>
		/// <exception cref="XmlException">If an error occured parsing the 
		/// RSS feed</exception>	
		public ArrayList GetItemsForFeed(feedsFeed f){
			//REM gets called from Bandit (retrive comment feeds)
			ArrayList returnList = EmptyItemList;

			if (this.offline)
				return returnList;

			ICredentials c = null;
			
			if(RssHelper.IsNntpUrl(f.link)){
				
				try{
					Uri feedUri = new Uri(f.link);						
						
					foreach(NntpServerDefinition nsd  in this.nntpServers){
						if(nsd.Server.Equals(feedUri.Authority)){
							c = this.GetNntpServerCredentials(nsd.Name);
							break;
						}
					}

				} catch (UriFormatException){;}
				
			}else{
				c = CreateCredentialsFrom(f);
			}
			

			using (Stream mem = AsyncWebRequest.GetSyncResponseStream(f.link, c, this.UserAgent, this.Proxy)) {
				if (RssParser.CanProcessUrl(f.link)) {
					returnList = RssParser.GetItemsForFeed(f, mem, false).itemsList; 
				}
			}

			return returnList;

		}

		/// <summary>
		/// Reads the RSS feed from the feedsFeed link then caches and returns the feed items 
		/// in an array list.
		/// </summary>
		/// <param name="feedUrl">The feed Url.</param>
		/// <returns>An arraylist of RSS items (i.e. instances of the NewsItem class)</returns>
		/// <exception cref="ApplicationException">If the RSS feed is not 
		/// version 0.91, 1.0 or 2.0</exception>
		/// <exception cref="XmlException">If an error occured parsing the 
		/// RSS feed</exception>	
		public  ArrayList GetItemsForFeed(string feedUrl){

			feedsFeed f = new feedsFeed();
			f.link = feedUrl;
			return this.GetItemsForFeed(f); 
		}

		/// <summary>
		/// Reads the feed from the stream then caches and returns the feed items 
		/// in an array list.
		/// </summary>
		/// <remarks>If the feedUrl is currently not stored in this object's internal table 
		///	then it is added/</remarks>		
		/// <param name="f">Information about the feed. This information is updated based
		/// on the results of processing the feed. </param>
		/// <param name="feedReader">A reader containing an feed.</param>				
		/// <param name="cachedStream">Flag states update last retrieved date on feed only 
		/// if the item was not cached. Indicates whether the lastretrieved date is updated
		/// on the feedsFeed object passed in. </param>
		/// <returns>A FeedDetails object which represents the feed</returns>
		/// <exception cref="ApplicationException">If the feed cannot be processed</exception>
		/// <exception cref="XmlException">If an error occured parsing the feed</exception>	
		public static IFeedDetails GetItemsForFeed(feedsFeed f, XmlReader feedReader, bool cachedStream) {
			//REM gets called from Bandit (AutoDiscoverFeedsThreadandler)
			if (f == null || f.link == null) 
				return null;

			if (RssParser.CanProcessUrl(f.link)) {
				return RssParser.GetItemsForFeed(f, feedReader, cachedStream); 																	
			}

			//TODO: NntpHandler.CanProcessUrl())
			throw new ApplicationException(ComponentsText.ExceptionNoProcessingHandlerMessage(f.link));

		}

		/// <summary>
		/// Reads a feed from the stream then caches and returns the feed items 
		/// in an array list.
		/// </summary>
		/// <remarks>If the feedUrl is currently not stored in this object's internal table 
		///	then it is added/</remarks>		
		/// <param name="f">Information about the feed. This information is updated based
		/// on the results of processing the feed. </param>
		/// <param name="feedStream">A stream containing an feed.</param>				
		/// <param name="cachedStream">Flag states update last retrieved date on feed only 
		/// if the item was not cached. Indicates whether the lastretrieved date is updated
		/// on the feedsFeed object passed in. </param>
		/// <returns>A FeedDetails object which represents the feed</returns>
		/// <exception cref="ApplicationException">If the feed cannot be processed</exception>
		/// <exception cref="XmlException">If an error occured parsing the RSS feed</exception>	
		//	[MethodImpl(MethodImplOptions.Synchronized)]
		public static IFeedDetails GetItemsForFeed(feedsFeed f, Stream feedStream, bool cachedStream) {

			if (f == null || f.link == null) 
				return null;

			if (RssParser.CanProcessUrl(f.link)) {
				return RssParser.GetItemsForFeed(f, feedStream, cachedStream); 																	
			}

			//TODO: NntpHandler.CanProcessUrl())
			throw new ApplicationException(ComponentsText.ExceptionNoProcessingHandlerMessage(f.link));
		}


		/// <summary>
		/// Reads the RSS feed from the stream then caches and returns the feed items 
		/// in an array list.
		/// </summary>
		/// <remarks>If the feedUrl is currently not stored in this object's internal table 
		///	then it is added/</remarks>
		/// <param name="feedUrl">The URL of the feed to download</param>
		/// <param name="feedStream">A stream containing an RSS feed.</param>
		/// <param name="id">A unique identifier for an RSS feed. This typically is the ETag returned if 
		/// the feed was fetched via HTTP.</param>
		/// <param name="cachedStream">Flag states update last retrieved date on feed only 
		/// if the item was not cached.</param>
		/// <exception cref="ApplicationException">If the RSS feed is not 
		/// version 0.91, 1.0 or 2.0</exception>
		/// <exception cref="XmlException">If an error occured parsing the 
		/// RSS feed</exception>	
		/// <returns>An arraylist of RSS items (i.e. instances of the NewsItem class)</returns>
		//	[MethodImpl(MethodImplOptions.Synchronized)]
		//DISCUSS: Not used. Can we remove?
		private ArrayList GetItemsForFeed(string  feedUrl, Stream  feedStream, string id, bool cachedStream) {

			feedsFeed f = FeedsTable[feedUrl];
			FeedInfo fi = RssParser.GetItemsForFeed(f, feedStream, cachedStream); 

			if(f.link == null){ f.containsNewMessages = false; }

			//add feed and related info to items table			
			lock(itemsTable) {
				if(itemsTable.ContainsKey(feedUrl)){
					itemsTable.Remove(feedUrl); 
				}

				itemsTable.Add(feedUrl, fi); 
			}

			if(id != null){
				f.etag = id; 
			}
						
			//return (ArrayList) fi.itemsList.Clone(); 			
			return fi.itemsList; 			
		}
	

		/// <summary>
		/// Retrieves the RSS feed for a particular subscription then converts 
		/// the blog posts or articles to an arraylist of items. 
		/// </summary>
		/// <param name="feedUrl">The URL of the feed to download</param>
		/// <param name="force_download">Flag indicates whether cached feed items 
		/// can be returned or whether the application must fetch resources from 
		/// the web</param>
		/// <exception cref="ApplicationException">If the RSS feed is not 
		/// version 0.91, 1.0 or 2.0</exception>
		/// <exception cref="XmlException">If an error occured parsing the 
		/// RSS feed</exception>
		/// <exception cref="WebException">If an error occurs while attempting to download from the URL</exception>
		/// <exception cref="UriFormatException">If an error occurs while attempting to format the URL as an Uri</exception>
		/// <returns>An arraylist of News items (i.e. instances of the NewsItem class)</returns>		
		//	[MethodImpl(MethodImplOptions.Synchronized)]
		public ArrayList GetItemsForFeed(string feedUrl, bool force_download){
			//REM gets called from Bandit
			string url2Access = feedUrl; 

			if(((!force_download)|| this.offline) && itemsTable.Contains(feedUrl)){
				return ((FeedDetailsInternal)itemsTable[feedUrl]).ItemsList;
			}
			
			//We need a reference to the feed so we can see if a cached object exists
			feedsFeed theFeed = null;
			if (FeedsTable.Contains(feedUrl)) 
				theFeed = FeedsTable[feedUrl];

			if (theFeed == null)	// not anymore in feedTable
				return EmptyItemList;

			try{ 
				if (theFeed != null) {
													
					if( ((!force_download) || this.offline) && (!itemsTable.ContainsKey(feedUrl)) && ((theFeed.cacheurl != null) && (theFeed.cacheurl.Length > 0) && (this.CacheHandler.FeedExists(theFeed)))) {						
						bool getFromCache = false;
						lock(itemsTable) {
							getFromCache= !itemsTable.ContainsKey(feedUrl);
						}
						if (getFromCache) {	// do not call from within a lock:
							IFeedDetails fi = this.GetFeed(theFeed);
							if (fi != null) {
								lock(itemsTable) {
									if (!itemsTable.ContainsKey(feedUrl))
										itemsTable.Add(feedUrl, fi);  
								}
							}
						}

						return ((FeedDetailsInternal)itemsTable[feedUrl]).ItemsList;
					}
				}

			}catch(Exception ex){
				Trace("Error retrieving feed '{0}' from cache: {1}" ,feedUrl, ex.ToString()); 
			}


			if(this.offline){ //we are in offline mode and don't have the feed cached. 
				return EmptyItemList; 
			}

			try {
				new Uri(url2Access);
			}				
			catch(UriFormatException ufex) {
				Trace("Uri format exception on '{0}': {1}" , url2Access, ufex.Message);
				throw;
			}
		

			this.AsyncGetItemsForFeed(feedUrl, true, true); 
			return EmptyItemList; //we just return this for now, the async call will return real results 
			
		}	
							
	

	
		/// <summary>
		/// Returns the number of pending async. requests in the queue.
		/// </summary>
		/// <returns></returns>
		public int AsyncRequestsPending() {
			return this.AsyncWebRequest.PendingRequests;
		}


		/// <summary>
		/// Creates a copy of the specified NewsItem with the specified feedsFeed as its owner 
		/// </summary>
		/// <param name="item">The item to copy</param>
		/// <param name="f">The owner feed</param>
		/// <returns>A copy of the specified news item</returns>
		public NewsItem CopyNewsItemTo(NewsItem item, feedsFeed f){
		
			//load item content from disk if not in memory, to get a full clone later on
			if(!item.HasContent) 
				this.GetCachedContentForItem(item); 
					
			// now create a full copy (including item content)
			return item.CopyTo(f);		
		}

		/// <summary>
		/// Loads the content of the NewsItem from the binary file containing 
		/// item content from disk. 
		/// </summary>
		/// <remarks>This should be called when a user clicks on an item which 
		/// had previously been read and thus wasn't loaded from disk on startup. </remarks>
		/// <param name="item"></param>
		public void GetCachedContentForItem(NewsItem item){
			this.CacheHandler.LoadItemContent(item); 			
		}
	  
		/// <summary>
		/// Retrieves items from local cache. 
		/// </summary>
		/// <param name="feedUrl"></param>
		/// <returns>A ArrayList of NewsItem objects</returns>
		public ArrayList GetCachedItemsForFeed(string feedUrl) {
		
			lock(itemsTable) {
				if ( itemsTable.Contains(feedUrl)) {
					return ((FeedInfo)itemsTable[feedUrl]).itemsList;
				}
			}
			
			//We need a reference to the feed so we can see if a cached object exists
			feedsFeed theFeed = FeedsTable[feedUrl];

			try{ 
				
				if (theFeed != null) {
													
					if( (theFeed.cacheurl != null) && (theFeed.cacheurl.Trim().Length > 0) &&
						(this.CacheHandler.FeedExists(theFeed))  ) {	
						bool getFromCache = false;
						lock(itemsTable) {
							getFromCache = !itemsTable.Contains(feedUrl);
						}
						if (getFromCache) {
							IFeedDetails fi = this.GetFeed(theFeed);
							if (fi != null) {
								lock(itemsTable) {
									if (!itemsTable.Contains(feedUrl)) 
										itemsTable.Add(feedUrl, fi);  
								}
							}
						}
						return ((FeedDetailsInternal)itemsTable[feedUrl]).ItemsList;
					}
				}
			}catch(FileNotFoundException){ // may be deleted in the middle of Test for Exists and GetFeed()
				// ignore
			}catch(XmlException xe){ //cached file is not well-formed so we remove it from cache. 	
				Trace("Xml Error retrieving feed '{0}' from cache: {1}", feedUrl, xe.ToString()); 
				this.CacheHandler.RemoveFeed(theFeed); 		  
			}catch(Exception ex){
				Trace("Error retrieving feed '{0}' from cache: {1}", feedUrl, ex.ToString()); 
				if (!theFeed.causedException) {        
					theFeed.causedException = true;   
					RaiseOnUpdateFeedException(feedUrl, new Exception("Error retrieving feed {" +  feedUrl + "} from cache: " + ex.Message, ex), 11);
				} 

			}

			return EmptyItemList;

		}

		/// <summary>
		/// Retrieves the RSS feed for a particular subscription then converts 
		/// the blog posts or articles to an arraylist of items. The http requests are async calls.
		/// </summary>
		/// <param name="feedUrl">The URL of the feed to download</param>
		/// <param name="force_download">Flag indicates whether cached feed items 
		/// can be returned or whether the application must fetch resources from 
		/// the web</param>
		/// <exception cref="ApplicationException">If the RSS feed is not 
		/// version 0.91, 1.0 or 2.0</exception>
		/// <exception cref="XmlException">If an error occured parsing the 
		/// RSS feed</exception>
		/// <exception cref="ArgumentNullException">If feedUrl is a null reference</exception>
		/// <exception cref="UriFormatException">If an error occurs while attempting to format the URL as an Uri</exception>
		/// <returns>true, if the request really was queued up</returns>
		/// <remarks>Result arraylist is returned by OnUpdatedFeed event within UpdatedFeedEventArgs</remarks>		
		//	[MethodImpl(MethodImplOptions.Synchronized)]
		public bool AsyncGetItemsForFeed(string feedUrl, bool force_download){
			return this.AsyncGetItemsForFeed(feedUrl, force_download, false);
		}
		/// <summary>
		/// Retrieves the RSS feed for a particular subscription then converts 
		/// the blog posts or articles to an arraylist of items. The http requests are async calls.
		/// </summary>
		/// <param name="feedUrl">The URL of the feed to download</param>
		/// <param name="force_download">Flag indicates whether cached feed items 
		/// can be returned or whether the application must fetch resources from 
		/// the web</param>
		/// <param name="manual">Flag indicates whether the call was initiated by user (true), or
		/// by automatic refresh timer (false)</param>
		/// <exception cref="ApplicationException">If the RSS feed is not version 0.91, 1.0 or 2.0</exception>
		/// <exception cref="XmlException">If an error occured parsing the RSS feed</exception>
		/// <exception cref="ArgumentNullException">If feedUrl is a null reference</exception>
		/// <exception cref="UriFormatException">If an error occurs while attempting to format the URL as an Uri</exception>
		/// <returns>true, if the request really was queued up</returns>
		/// <remarks>Result arraylist is returned by OnUpdatedFeed event within UpdatedFeedEventArgs</remarks>		
		//	[MethodImpl(MethodImplOptions.Synchronized)]
		public bool AsyncGetItemsForFeed(string feedUrl, bool force_download, bool manual){
			
			if (feedUrl == null || feedUrl.Trim().Length == 0)
				throw new ArgumentNullException("feedUrl");

			Uri reqUri = null;

			string etag = null; 
			DateTime lastModified = DateTime.MinValue; 
			bool requestQueued = false;

			int priority = 10;
			if (force_download)
				priority += 100;
			if (manual) 
				priority += 1000;


			try{

				reqUri = new Uri(feedUrl);		

				try{

					if ((!force_download) || this.offline) {
						GetCachedItemsForFeed(feedUrl); //load feed into itemsTable
						RaiseOnUpdatedFeed(reqUri, null,  RequestResult.NotModified, priority, false);
						return false;
					}

				}catch(XmlException xe){ //cache file is corrupt
					Trace("Unexpected error retrieving cached feed '{0}': {1}" ,feedUrl , xe.ToString());
				}

				//We need a reference to the feed so we can see if a cached object exists
				feedsFeed theFeed = null;
				if (FeedsTable.ContainsKey(feedUrl)) 
					theFeed = FeedsTable[feedUrl];

				if (theFeed == null)
					return false;			
			 	     
	
				// only if we "real" go over the wire for an update:
				RaiseOnUpdateFeedStarted(reqUri, force_download, priority);

				//DateTime lastRetrieved = DateTime.MinValue; 
				lastModified = DateTime.MinValue;

				if(itemsTable.Contains(feedUrl)){
					etag = theFeed.etag; 
					lastModified = ( theFeed.lastretrievedSpecified ? theFeed.lastretrieved : theFeed.lastmodified);					
				}


				ICredentials c = null;								  				

				//get credentials from server definition if this is a newsgroup subscription
				if(RssHelper.IsNntpUrl(theFeed.link)){
					c = GetNntpServerCredentials(theFeed);
				}else{
					c = CreateCredentialsFrom(theFeed);
				}

				RequestParameter reqParam = RequestParameter.Create(reqUri, this.UserAgent, this.Proxy, c, lastModified, etag);
				// global cookie handling:
				reqParam.SetCookies = NewsHandler.SetCookies;
				
				AsyncWebRequest.QueueRequest(reqParam, 
					null /* new RequestQueuedCallback(this.OnRequestQueued) */, 
					new RequestStartCallback(this.OnRequestStart), 
					new RequestCompleteCallback(this.OnRequestComplete), 
					new RequestExceptionCallback(this.OnRequestException), priority);
				
				requestQueued = true;
				
			} catch (Exception e) { 
					
				Trace("Unexpected error on QueueRequest(), processing feed '{0}': {1}" , feedUrl, e.ToString());												
				RaiseOnUpdateFeedException(feedUrl, e, priority);
			}			
			
			return requestQueued;
		}

		#region GetFailureContext()
		/// <summary>
		/// Populates a hashtable with additional feed infos 
		/// we need to provide useful error infos to a user.
		/// It is only fully populated, if we have it allready read from cache.
		/// </summary>
		/// <remarks>
		/// Currently we populate the following keys:
		/// * TECH_CONTACT	(opt.; mail address from: 'webMaster' (RSS) or 'errorReportsTo' (Atom) )
		/// * PUBLISHER			(opt.; mail address from: 'managingEditor' (RSS)
		/// * PUBLISHER_HOMEPAGE	(opt.; additional info link)
		/// * GENERATOR			(opt.; generator software)
		/// * FULL_TITLE			(allways there; category and title as it is used in the UI)
		/// * FAILURE_OBJECT 	(allways there; feedsFeed | nntpFeed)
		/// </remarks>
		/// <param name="feedUri">Uri</param>
		/// <returns>Hashtable</returns>
		public Hashtable GetFailureContext(Uri feedUri) {
			if (feedUri == null)
				return new Hashtable();
			return this.GetFailureContext(FeedsTable[feedUri]);
		}


		/// <summary>
		/// Overloaded.
		/// </summary>
		/// <param name="feedUri">The feed URI.</param>
		/// <returns></returns>
		public Hashtable GetFailureContext(string feedUri) {
			if (feedUri == null)
				return new Hashtable();
			return this.GetFailureContext(FeedsTable[feedUri]);
		}

		/// <summary>
		/// Overloaded.
		/// </summary>
		/// <param name="f"></param>
		/// <returns></returns>
		public Hashtable GetFailureContext(feedsFeed f) {
			
			if (f == null) {	// how about nntpFeeds? They are within the FeedsTable (with different class type)?
				return new Hashtable();
			}

			FeedInfo fi = null;
			lock(itemsTable) {
				if (itemsTable.ContainsKey(f.link)) {
					fi = itemsTable[f.link] as FeedInfo;
				}
			}

			return NewsHandler.GetFailureContext(f, fi);
		}
		
		/// <summary>
		/// Overloaded.
		/// </summary>
		/// <param name="f"></param>
		/// <param name="fi"></param>
		/// <returns></returns>
		public static Hashtable GetFailureContext(feedsFeed f, IFeedDetails fi) {
			
			Hashtable context = new Hashtable();
			
			if (f == null) {	
				return context;
			}

			context.Add("FULL_TITLE", (f.category != null ? f.category: String.Empty) + NewsHandler.CategorySeparator +  f.title);
			context.Add("FAILURE_OBJECT", f);

			if (fi == null) 
				return context;

			context.Add("PUBLISHER_HOMEPAGE", fi.Link);
			
			XmlElement xe = RssHelper.GetOptionalElement(fi.OptionalElements, "managingEditor", String.Empty);
			if (xe != null)
				context.Add("PUBLISHER", xe.InnerText);

			xe = RssHelper.GetOptionalElement(fi.OptionalElements, "webMaster",  String.Empty);
			if (xe != null){					
				context.Add("TECH_CONTACT", xe.InnerText);
			}else{
				xe = RssHelper.GetOptionalElement(fi.OptionalElements, "errorReportsTo", "http://webns.net/mvcb/");
				if (xe != null && xe.Attributes["resource", "http://www.w3.org/1999/02/22-rdf-syntax-ns#"] != null)											
					context.Add("TECH_CONTACT", xe.Attributes["resource", "http://www.w3.org/1999/02/22-rdf-syntax-ns#"].InnerText);
			}

			xe = RssHelper.GetOptionalElement(fi.OptionalElements, "generator", String.Empty);
			if (xe != null)
				context.Add("GENERATOR", xe.InnerText);

			return context;
		}
		#endregion

		//no really used,...:
		//		private void OnRequestQueued(Uri requestUri, int priority) {
		//			//Trace.WriteLineIf(TraceMode, "Queued: '"+ requestUri.ToString() + "', with Priority '" + priority.ToString()+ "'...", "AsyncRequest");
		//		}

		private void OnRequestStart(Uri requestUri, ref bool cancel) {
			Trace("AsyncRequest.OnRequestStart('{0}') downloading", requestUri.ToString());
			this.RaiseOnDownloadFeedStarted(requestUri, ref cancel);
			if (!cancel)
				cancel = this.Offline;
		}

		private void OnRequestException(Uri requestUri, Exception e, int priority) {
		  
			Trace("AsyncRequst.OnRequestException() fetching '{0}': {1}", requestUri.ToString(), e.ToString()); 

			if(this.FeedsTable.Contains(requestUri)){
				Trace("AsyncRequest.OnRequestException() '{0}' found in feedsTable.", requestUri.ToString()); 
				feedsFeed f = FeedsTable[requestUri]; 
				// now we set this within causedException prop.
				//f.lastretrieved = DateTime.Now; 
				//f.lastretrievedSpecified = true; 
				f.causedException = true;
			} else {
				Trace("AsyncRequst.OnRequestException() '{0}' NOT found in feedsTable.", requestUri.ToString()); 
			}

			RaiseOnUpdateFeedException(requestUri.AbsoluteUri, e, priority);
		}

		private void OnRequestComplete(Uri requestUri, Stream response, Uri newUri, string eTag, DateTime lastModified, RequestResult result, int priority) {
			Trace("AsyncRequest.OnRequestComplete: '{0}': {1}", requestUri.ToString(), result );
			if (newUri != null)
				Trace("AsyncRequest.OnRequestComplete: perma redirect of '{0}' to '{1}'.", requestUri.ToString(), newUri.ToString() );

			ArrayList itemsForFeed = new ArrayList(); 
			bool firstSuccessfulDownload = false; 
		  
			//grab items from feed, then save stream to cache. 
			try{

				//We need a reference to the feed so we can see if a cached object exists
				feedsFeed theFeed = FeedsTable[requestUri];

				if (theFeed == null) {
					Trace("ATTENTION! FeedsTable[requestUri] as feedsFeed returns null for: '{0}'", requestUri.ToString());
					return;
				}

				string feedUrl = theFeed.link;
				if (true) {
					if (String.Compare(feedUrl, requestUri.AbsoluteUri, true) != 0)
						Trace("feed.link != requestUri: \r\n'{0}'\r\n'{1}'", feedUrl, requestUri.AbsoluteUri);
				}

				if (newUri != null) {	// Uri changed/moved permanently

					FeedsTable.Remove(feedUrl); 
					theFeed.link = newUri.AbsoluteUri; 
					FeedsTable.Add(theFeed.link, theFeed); 
						
					lock(itemsTable) {
						if(itemsTable.Contains(feedUrl)){
							object FI = itemsTable[feedUrl];
							itemsTable.Remove(feedUrl); 
							itemsTable.Remove(theFeed.link); //remove any old cached versions of redirected link
							itemsTable.Add(theFeed.link, FI); 
						}
					}

					feedUrl = theFeed.link;

				}	// newUri

				if (result == RequestResult.OK) {
					//Update our recently read stories. This is very necessary for 
					//dynamically generated feeds which always return 200(OK) even if unchanged							
						
					FeedDetailsInternal fi = null; 

					if((requestUri.Scheme == NntpWebRequest.NntpUriScheme) || (requestUri.Scheme == NntpWebRequest.NewsUriScheme)){
						fi = NntpParser.GetItemsForNewsGroup(theFeed, response, false);							 					
					}else{
						fi = RssParser.GetItemsForFeed(theFeed, response, false);							 
					}
					
					FeedDetailsInternal fiFromCache = null; 
				   
					// Sometimes we may not have loaded feed from cache. So ensure it is 
					// loaded into memory if cached. We don't lock here because loading from
					// disk is too long a time to hold a lock.  
					try{
					
						if(!itemsTable.ContainsKey(feedUrl)){
							fiFromCache = this.GetFeed(theFeed); 					 
						}
					}catch(Exception ex){ 
						Trace("this.GetFeed(theFeed) caused exception: {0}", ex.ToString());
						/* the cache file may be corrupt or an IO exception 
						 * not much we can do so just ignore it 
						 */					
					}

					IList newReceivedItems = null;
					
					//Merge items list from cached copy of feed with this newly fetched feed. 
					//Thus if a feed removes old entries (such as a news site with daily updates) we 
					//don't lose them in the aggregator. 
					lock(itemsTable){	//TODO: resolve time consuming lock to hold only a short time!!!
				 		
						//if feed was in cache but not in itemsTable we load it into itemsTable
						if(!itemsTable.ContainsKey(feedUrl) && (fiFromCache!= null)){
							itemsTable.Add(feedUrl, fiFromCache); 
						}

						if(itemsTable.ContainsKey(feedUrl)){	
								
							FeedDetailsInternal fi2    = (FeedDetailsInternal) itemsTable[feedUrl];														

							if (RssParser.CanProcessUrl(feedUrl)) {
							
								fi.ItemsList = (ArrayList) MergeAndPurgeItems(fi2.ItemsList, fi.ItemsList, theFeed.deletedstories,
																				out newReceivedItems, theFeed.replaceitemsonrefresh);																								
							} 
						
							//fi.MaxItemAge = fi2.MaxItemAge;						 
							
							/*
							 * HACK: We have an issue that OnRequestComplete is sometimes passed a response Stream 
							 * that doesn't match the requestUri. We insert a test here to see if this has occured
							 * and if so we return from this method.  
							 * 
							 * We are careful here to ensure we don't treat a case of the feed or website being moved 
							 * as an instance of this bug. We do this by (1) test to see if website URL in feed just 
							 * downloaded matches the site URL in the feed from the cache AND (2) if all the items in
							 * the feed we just downloaded were never in the cache AND (3) the site URL is the same for
							 * the site URL for another feed we have in the cache. 
							 */ 
							if((String.Compare(fi2.Link, fi.Link, true) != 0) && (newReceivedItems.Count == fi.ItemsList.Count)){							
								foreach(FeedDetailsInternal fdi in itemsTable.Values){ 
									if(String.Compare(fdi.Link, fi.Link, true) == 0){								
										RaiseOnUpdatedFeed(requestUri, null, RequestResult.NotModified, priority, false);
										_log.Error(String.Format("Feed mixup encountered when downloading {2} because fi2.link != fi.link: {0}!= {1}", fi2.Link, fi.Link, requestUri.AbsoluteUri));						
										return;
									}
								}//foreach
							}

							itemsTable.Remove(feedUrl); 
						}else{ //if(itemsTable.ContainsKey(feedUrl)){
							firstSuccessfulDownload = true;
							newReceivedItems = fi.ItemsList;
						}

						itemsTable.Add(feedUrl, fi); 
					}//lock(itemsTable)					    

					//if(eTag != null){	// why we did not store the null?
					theFeed.etag = eTag; 
					//}

					if (lastModified > theFeed.lastmodified) {
						theFeed.lastmodified = lastModified;
					}
			
					theFeed.lastretrieved = new DateTime(DateTime.Now.Ticks); 
					theFeed.lastretrievedSpecified = true; 
								
					theFeed.cacheurl = this.SaveFeed(theFeed); 							
					this.SearchHandler.IndexAdd(newReceivedItems);	// may require theFeed.cacheurl !
					
					theFeed.causedException = false;
					itemsForFeed = fi.ItemsList; 

					/* download podcasts from items we just received if downloadenclosures == true */
					if(this.GetDownloadEnclosures(theFeed.link)){
						int numDownloaded = 0; 
						int maxDownloads  = (firstSuccessfulDownload ? this.NumEnclosuresToDownloadOnNewFeed : Int32.MaxValue);

						foreach(NewsItem ni in newReceivedItems){
							
							//ensure that we don't attempt to download these enclosures at a later date
							if(numDownloaded >= maxDownloads) {
								MarkEnclosuresDownloaded(ni);
								continue;
							}

							try{
								numDownloaded += this.DownloadEnclosure(ni, maxDownloads - numDownloaded); 							 							
							}catch(DownloaderException de){
								_log.Error("Error occured when downloading enclosures in OnRequestComplete():", de);
							}
						}
					}

					/* Make sure read stories are accurately calculated */ 
					theFeed.containsNewMessages = false; 
					theFeed.storiesrecentlyviewed.Clear();
	 
					foreach(NewsItem ri in itemsForFeed){
						if(ri.BeenRead){
							theFeed.storiesrecentlyviewed.Add(ri.Id); 
						}

						if(ri.HasNewComments){
							theFeed.containsNewComments = true; 
						}
					}
								

					if(itemsForFeed.Count > theFeed.storiesrecentlyviewed.Count){
						theFeed.containsNewMessages = true; 
					} 

				} else if (result == RequestResult.NotModified) {

					// expected behavior: response == null, if not modified !!!
					theFeed.lastretrieved = new DateTime(DateTime.Now.Ticks); 
					theFeed.lastretrievedSpecified = true; 
					theFeed.causedException = false;

					FeedDetailsInternal feedInfo = (FeedDetailsInternal)itemsTable[feedUrl];
					if (feedInfo != null)
						itemsForFeed = feedInfo.ItemsList;
					else 
						itemsForFeed = EmptyItemList;
			  
				} else {
					throw new NotImplementedException("Unhandled RequestResult: " + result.ToString());
				}

				RaiseOnUpdatedFeed(requestUri, newUri, result, priority, firstSuccessfulDownload);

			}catch(Exception e){
			  
				if(this.FeedsTable.Contains(requestUri)){
					Trace("AsyncRequest.OnRequestComplete('{0}') Exception: ", requestUri.ToString(), e.StackTrace); 
					feedsFeed f = FeedsTable[requestUri]; 
					// now we set this within causedException prop.:
					//f.lastretrieved = DateTime.Now; 
					//f.lastretrievedSpecified = true; 
					f.causedException = true;
				} else {
					Trace("AsyncRequest.OnRequestComplete('{0}') Exception on feed not contained in FeedsTable: ", requestUri.ToString(), e.StackTrace); 
				}
				
				RaiseOnUpdateFeedException(requestUri.AbsoluteUri, e, priority);

			} finally {
				if (response != null)
					response.Close();
			}

		}
		
		// used as AsyncWebRequest.RequestAllCompleteCallback
		private void OnAllRequestsComplete() {
			RaiseOnAllAsyncRequestsCompleted();
		}


		private void OnEnclosureDownloadComplete(object sender, DownloadItemEventArgs e){
			if(this.OnDownloadedEnclosure != null){
				try{
					this.OnDownloadedEnclosure(sender, e);
				} catch { /* ignore ex. thrown by callback */  }
			}		
		}

		// see http://www.iana.org/assignments/media-types/image/vnd.microsoft.icon
		private static readonly byte[] ico_magic = new byte[]{0,0,1,0};
		private static readonly int ico_magic_len = ico_magic.Length;
		private static readonly byte[] png_magic = new byte[]{0x89,0x50,0x4e,0x47};
		private static readonly int png_magic_len = png_magic.Length;
		private static readonly byte[] gif_magic = new byte[]{0x47,0x49,0x46};
		private static readonly int gif_magic_len = gif_magic.Length;
		private static readonly byte[] jpg_magic = new byte[]{0xff, 0xd8};
		private static readonly int jpg_magic_len = jpg_magic.Length;
		private static readonly byte[] bmp_magic = new byte[]{0x42, 0x4d};
		private static readonly int bmp_magic_len = bmp_magic.Length;

		
		
		/// <summary>
		/// Gets the file extension for a detected image 
		/// </summary>
		/// <param name="bytes">Not null and length > 4!</param>
		/// <returns></returns>
		private static string GetExtensionForDetectedImage(byte[] bytes) {
			if (bytes == null)
				throw new ArgumentNullException("bytes");
			int i, len = bytes.Length;

			//check for jpg magic: 
			for (i=0; i < jpg_magic_len && i < len; i++) {
				if (bytes[i] != jpg_magic[i]) break;
			}
			if (i == jpg_magic_len) return ".jpg";

			// check for ico magic:
			for (i=0; i < ico_magic_len && i < len; i++) {
				if (bytes[i] != ico_magic[i]) break;
			}
			if (i == ico_magic_len) return ".ico";
			
			// check for png magic:
			for (i=0; i < png_magic_len && i < len; i++) {
				if (bytes[i] != png_magic[i]) break;
			}
			if (i == png_magic_len) return ".png";

			// check for gif magic:
			for (i=0; i < gif_magic_len && i < len; i++) {
				if (bytes[i] != gif_magic[i]) break;
			}
			if (i == gif_magic_len) return ".gif";

			// check for bmp magic:
			for (i=0; i < bmp_magic_len && i < len; i++) {
				if (bytes[i] != bmp_magic[i]) break;
			}
			if (i == bmp_magic_len) return ".bmp";

			// not supported, or <HTML> reporting a failure:
			return null;
		}
		
		private void OnFaviconRequestComplete(Uri requestUri, Stream response, Uri newUri, string eTag, DateTime lastModified, RequestResult result, int priority) {
			
			
			
			Trace("AsyncRequest.OnFaviconRequestComplete: '{0}': {1}", requestUri.ToString(), result );
			if (newUri != null)
				Trace("AsyncRequest.OnFaviconRequestComplete: perma redirect of '{0}' to '{1}'.", requestUri.ToString(), newUri.ToString() );

			try{

				StringCollection  feedUrls = new StringCollection(); 
				string favicon = null;

				if (result == RequestResult.OK) {
					
					//write favicon to feed cache location 
					BinaryReader br = new BinaryReader(response); 
					byte[] bytes = new byte[response.Length];
					// don't write null length files:
					if (bytes.Length > 0) {
						bytes = br.ReadBytes((int) response.Length);
						// check for some known common image formats:
						string ext	 = GetExtensionForDetectedImage(bytes);
						if (ext != null) {
							favicon = GenerateFaviconUrl(requestUri, ext);
							string filelocation = Path.Combine(this.CacheHandler.CacheLocation, favicon);

							using (FileStream fs = FileHelper.OpenForWrite(filelocation)) {
								BinaryWriter bw = new BinaryWriter(fs);
								bw.Write(bytes);
								bw.Flush();
							}
						}
					} else {
						// favicon == null; reset
					}

					// The "CopyTo()" construct prevents against InvalidOpExceptions/ArgumentOutOfRange
					// exceptions and keep the loop alive if FeedsTable gets modified from other thread(s)
					string[] keys;
			
					lock (FeedsTable.SyncRoot) {
						keys = new string[FeedsTable.Count];
						if (FeedsTable.Count > 0)
							FeedsTable.Keys.CopyTo(keys, 0);	
					}

					//get all feeds that should use the returned favicon
					foreach(string feedUrl in keys){						

						if(itemsTable.ContainsKey(feedUrl)){

							string websiteUrl = ((FeedInfo)itemsTable[feedUrl]).Link;

							Uri uri = null; 
							try{ uri = new Uri(websiteUrl); }catch(Exception){;}
						
							if((uri != null) && uri.Authority.Equals(requestUri.Authority)){
								feedUrls.Add(feedUrl);
								feedsFeed f = FeedsTable[feedUrl];
								f.favicon = favicon; 
							}
						}
					}//foreach

				}

				if(favicon != null){
					RaiseOnUpdatedFavicon(favicon, feedUrls);
				}
				
			}catch(Exception e){			  
				
				Trace("AsyncRequest.OnFaviconRequestComplete('{0}') Exception on fetching favicon at: ", requestUri.ToString(), e.StackTrace); 								

			} finally {
				if (response != null)
					response.Close();
			}

		}

		private void RaiseOnDownloadFeedStarted(Uri requestUri, ref bool cancel) {
			if (BeforeDownloadFeedStarted != null) {
				try {
					DownloadFeedCancelEventArgs ea = new DownloadFeedCancelEventArgs(requestUri, cancel);
					BeforeDownloadFeedStarted(this, ea);
					cancel = ea.Cancel;
				} catch { /* ignore ex. thrown by callback */  }
			}
		}

		private void RaiseOnUpdatedFavicon(string favicon, StringCollection feedUrls) {
			if (OnUpdatedFavicon != null) {
				try {
					OnUpdatedFavicon(this, new UpdatedFaviconEventArgs(favicon, feedUrls));
				} catch { /* ignore ex. thrown by callback */  }
			}
		}


		private void RaiseOnUpdatedFeed(Uri requestUri, Uri newUri, RequestResult result, int priority, bool firstSuccessfulDownload) {
			if (OnUpdatedFeed != null) {
				try {
					OnUpdatedFeed(this, new UpdatedFeedEventArgs(requestUri, newUri, result, priority, firstSuccessfulDownload));
				} catch { /* ignore ex. thrown by callback */  }
			}
		}

		/* private void RaiseOnUpdateFeedException(Uri requestUri, Exception e, int priority) {
			if (OnUpdateFeedException != null) {
				try {
					if (requestUri != null && RssParser.CanProcessUrl(requestUri.ToString()))
						e = new FeedRequestException(e.Message, e, this.GetFailureContext(requestUri)); 
					OnUpdateFeedException(this, new UpdateFeedExceptionEventArgs(requestUri, e, priority));
				} catch { /* ignore ex. thrown by callback   }
			}
		} */

		private void RaiseOnUpdateFeedException(string requestUri, Exception e, int priority) {
			if (OnUpdateFeedException != null) {
				try {
					if (requestUri != null && RssParser.CanProcessUrl(requestUri))
						e = new FeedRequestException(e.Message, e, this.GetFailureContext(requestUri)); 
					OnUpdateFeedException(this, new UpdateFeedExceptionEventArgs(requestUri, e, priority));
				} catch { /* ignore ex. thrown by callback */  }
			}
		}

		private void RaiseOnAllAsyncRequestsCompleted() {
			if (OnAllAsyncRequestsCompleted != null) {
				try {
					OnAllAsyncRequestsCompleted(this, new EventArgs());
				} catch {/* ignore ex. thrown by callback */ }
			}
		}

		private void RaiseOnUpdateFeedsStarted(bool forced) {
			if ( UpdateFeedsStarted != null) {
				try {
					UpdateFeedsStarted(this, new UpdateFeedsEventArgs(forced));
				} catch {/* ignore ex. thrown by callback */ }
			}
		}
	 
		private void RaiseOnUpdateFeedStarted(Uri feedUri, bool forced, int priority) {
			if ( UpdateFeedStarted != null) {
				try {
					UpdateFeedStarted(this, new UpdateFeedEventArgs(feedUri, forced, priority));
				} catch {/* ignore ex. thrown by callback */ }
			}
		}

		/// <summary>
		/// Uses a deterministic algorithm to generate a name for a favicon file from
		/// the domain name of the site that it belongs to.
		/// </summary>
		/// <param name="uri">The URL to the favicon</param>
		/// <param name="extension">The file extension.</param>
		/// <returns>A name for the favicon file</returns>
		private static string GenerateFaviconUrl(Uri uri, string extension){		
			return uri.Authority.Replace(".","-") + extension; 
		}


		/// <summary>
		/// Determines whether the file should be treated as a podcast or just as a regular enclosure.
		/// </summary>
		/// <param name="filename">The name of the file</param>
		/// <returns>Returns true if the file extension is one of those in the podcastfileextensions ArrayList</returns>
		public bool IsPodcast(string filename){	
			if(StringHelper.EmptyOrNull(filename)){ return false;}
				
			string fileext = Path.GetExtension(filename);
			
			if(fileext.Length > 1){
				fileext = fileext.Substring(1);
				
				foreach(string podcastExt in this.podcastfileextensions){
					if(fileext.ToLower().Equals(podcastExt.ToLower())){
						return true; 
					}
				}//foreach
			}

			return false; 
		}

		/// <summary>
		/// Helper function that marks all of an items enclosures as downloaded. 
		/// </summary>
		/// <param name="item"></param>
		private static void MarkEnclosuresDownloaded(NewsItem item){
			
			if(item == null) { return; }

			foreach(Enclosure enc in item.Enclosures){				
					enc.Downloaded = true; 				
			}		
		}

		/// <summary>
		/// Downloads all the enclosures associated with the specified NewsItem
		/// </summary>
		/// <param name="item">The newsitem whose enclosures are being downloaded</param>
		/// <param name="maxNumToDownload">The maximum number of enclosures that can be downloaded from this item</param>
		/// <returns>The number of downloaded enclosures</returns>
		private int DownloadEnclosure(NewsItem item, int maxNumToDownload){

			int numDownloaded = 0; 			

			if((maxNumToDownload > 0) && (item != null) && (item.Enclosures.Count > 0)){
				foreach(Enclosure enc in item.Enclosures){
					DownloadItem di = new DownloadItem(item.Feed.link, item.Id, enc, this.enclosureDownloader);
					
					if(!enc.Downloaded){
						this.enclosureDownloader.BeginDownload(di); 	
						enc.Downloaded = true; 
						numDownloaded++; 
					}
					if(numDownloaded >= maxNumToDownload) break; 
				}
			}//if
		
			if(numDownloaded < item.Enclosures.Count){
				MarkEnclosuresDownloaded(item); 
			}

			return numDownloaded;
		}


		/// <summary>
		/// Downloads all the enclosures associated with the specified NewsItem
		/// </summary>
		/// <param name="item">The newsitem whose enclosures are being downloaded</param>
		public void DownloadEnclosure(NewsItem item){
			this.DownloadEnclosure(item, Int32.MaxValue);	
		}

		/// <summary>
		/// Download the specified enclosure associated with the specified NewsItem. 
		/// </summary>
		/// <remarks>The enclosure will be downloaded ONLY IF it is found as the Url 
		/// field of one of the Enclosure objects in the Enclosures collection of the specified NewsItem</remarks>
		/// <param name="item"></param>
		/// <param name="fileName">The name of the enclosure file to download</param>
		public void DownloadEnclosure(NewsItem item, string fileName){					 

			if((item != null) && (item.Enclosures.Count > 0)){

				foreach(Enclosure enc in item.Enclosures){
					if(enc.Url.EndsWith(fileName)){						
						DownloadItem di = new DownloadItem(item.Feed.link, item.Id, enc, this.enclosureDownloader);
						this.enclosureDownloader.BeginDownload(di); 				
						enc.Downloaded = true; 
						break;
					}
				}//foreach										
			}//if(item != null && ...)

		}

		/// <summary>
		/// Resumes pending BITS downloads from a if any exist. 
		/// </summary>
		public void ResumePendingDownloads(){
			this.enclosureDownloader.ResumePendingDownloads();
		}

		/// <summary>
		/// Downloads the favicons for the various feeds. 
		/// </summary>
		public void RefreshFavicons(){

			if((this.FeedsListOK == false) || this.offline){ //we don't have a feed list
				return; 
			}
			
			System.Collections.Specialized.StringCollection websites = new System.Collections.Specialized.StringCollection();

			try{ 

				// The "CopyTo()" construct prevents against InvalidOpExceptions/ArgumentOutOfRange
				// exceptions and keep the loop alive if itemsTable gets modified from other thread(s)
				string[] keys;
			
				lock (itemsTable.SyncRoot) {
					keys = new string[itemsTable.Count];
					if (itemsTable.Count > 0)
						itemsTable.Keys.CopyTo(keys, 0);	
				}

				//foreach(string sKey in FeedsTable.Keys){
				//  feedsFeed current = FeedsTable[sKey];	

				for(int i = 0, len = keys.Length; i < len; i++){

				FeedInfo fi =  (FeedInfo) itemsTable[keys[i]]; 
					
						Uri webSiteUrl = null;
					try{ 
						webSiteUrl = new Uri(fi.link); 
					}catch(Exception){;}
						
					if(webSiteUrl == null || !webSiteUrl.Scheme.ToLower().Equals("http")){
						continue; 
					}
	
					if(!websites.Contains(webSiteUrl.Authority)){
						UriBuilder reqUri = new UriBuilder("http", webSiteUrl.Authority);
						reqUri.Path       = "favicon.ico";
							
						RequestParameter reqParam = RequestParameter.Create(reqUri.Uri, this.UserAgent, this.Proxy, 
																	/* ICredentials */ null, 
																	/* lastModified */ DateTime.MinValue, 
																	/* etag */ null);
						// global cookie handling:
						reqParam.SetCookies = NewsHandler.SetCookies;
				
						AsyncWebRequest.QueueRequest(reqParam, 
							null /* new RequestQueuedCallback(this.OnRequestQueued) */, 
							null /* new RequestStartCallback(this.OnRequestStart) */, 
							new RequestCompleteCallback(this.OnFaviconRequestComplete), 
							null /* new RequestExceptionCallback(this.OnRequestException) */, 
							100  /* priority*/ );
						
						websites.Add(webSiteUrl.Authority); 

					}//if(!websites.Contains(webSiteUrl.Authority)){					
				}//foreach(FeedInfo fi in itemsTable.Values){

			}catch(InvalidOperationException ioe){// New feeds added to FeedsTable from another thread  
							
				Trace("RefreshFavicons() InvalidOperationException: {0}", ioe.ToString()); 
			} 
		}


		/// <summary>
		/// Downloads every feed that has either never been downloaded before or 
		/// whose elapsed time since last download indicates a fresh attempt should be made. 
		/// </summary>
		/// <param name="force_download">A flag that indicates whether download attempts should be made 
		/// or whether the cache can be used.</param>
		/// <remarks>This method uses the cache friendly If-None-Match and If-modified-Since
		/// HTTP headers when downloading feeds.</remarks>	
		public void RefreshFeeds(bool force_download){		

			if(this.FeedsListOK == false){ //we don't have a feed list
				return; 
			}

			bool anyRequestQueued = false;

			try{ 
			
				RaiseOnUpdateFeedsStarted(force_download);

				// The "CopyTo()" construct prevents against InvalidOpExceptions/ArgumentOutOfRange
				// exceptions and keep the loop alive if FeedsTable gets modified from other thread(s)
				string[] keys;
			
				lock (FeedsTable.SyncRoot) {
					keys = new string[FeedsTable.Count];
					if (FeedsTable.Count > 0)
						FeedsTable.Keys.CopyTo(keys, 0);	
				}

				//foreach(string sKey in FeedsTable.Keys){
				//  feedsFeed current = FeedsTable[sKey];	

				for(int i = 0, len = keys.Length; i < len; i++){

					feedsFeed current = FeedsTable[keys[i]];	
				
					if (current == null)	// may have been redirected/removed meanwhile
						continue;

					try{ 

						// new: giving up after ten unsuccessfull requests
						if (!force_download && current.causedExceptionCount >= 10) {
							continue;
						}

						if(current.refreshrateSpecified && (current.refreshrate == 0)){
							continue; 	    
						}

						if(itemsTable.ContainsKey(current.link)){ //check if feed downloaded in the past
					
							//check if enough time has elapsed as to require a download attempt
							if((!force_download) && current.lastretrievedSpecified){
		
								double timeSinceLastDownload = DateTime.Now.Subtract(current.lastretrieved).TotalMilliseconds;
								int refreshRate           =  current.refreshrateSpecified ?  current.refreshrate : this.RefreshRate; 
		
								if(timeSinceLastDownload < refreshRate){
									continue; //no need to download 
								}	 																		
							}//if(current.lastretrievedSpecified...) 

	      
							if (this.AsyncGetItemsForFeed(current.link, true, false))
								anyRequestQueued = true;
	    
						}else{	
							
							// not yet loaded, so not loaded from cache, new subscribed or imported
							if (current.lastretrievedSpecified && StringHelper.EmptyOrNull(current.cacheurl))  {	
								// imported may have lastretrievedSpecified set to reduce the initial payload
								double timeSinceLastDownload = DateTime.Now.Subtract(current.lastretrieved).TotalMilliseconds;
								int refreshRate           =  current.refreshrateSpecified ?  current.refreshrate : this.RefreshRate; 
		
								if(timeSinceLastDownload < refreshRate){
									continue; //no need to download 
								} 
							}
							
							if (!force_download) {
								// not in itemsTable, cacheurl set - but no cache file anymore?
								if (!StringHelper.EmptyOrNull(current.cacheurl) && 
								    !this.CacheHandler.FeedExists(current))
									force_download = true;
							}
							
							if (this.AsyncGetItemsForFeed(current.link, force_download, false))
								anyRequestQueued = true;
							
						}

						Thread.Sleep(15);	// force a context switches

					}catch(Exception e){ 

						Trace("RefreshFeeds(bool) unexpected error processing feed '{0}': {1}", keys[i], e.ToString()); 
	  
					}	  									

				}//for(i)

			}catch(InvalidOperationException ioe){// New feeds added to FeedsTable from another thread  
							
				Trace("RefreshFeeds(bool) InvalidOperationException: {0}", ioe.ToString()); 
			} 							
			finally {
				if (offline || !anyRequestQueued)
					RaiseOnAllAsyncRequestsCompleted();
			}
		}

		/// <summary>
		/// Downloads every feed that has either never been downloaded before or 
		/// whose elapsed time since last download indicates a fresh attempt should be made. 
		/// </summary>
		/// <param name="category">Refresh all feeds, that are part of the category</param>
		/// <param name="force_download">A flag that indicates whether download attempts should be made 
		/// or whether the cache can be used.</param>
		/// <remarks>This method uses the cache friendly If-None-Match and If-modified-Since
		/// HTTP headers when downloading feeds.</remarks>	
		public void RefreshFeeds(string category, bool force_download){		

			if(this.FeedsListOK == false){ //we don't have a feed list
				return; 
			}

			bool anyRequestQueued = false;

			try{ 
			
				RaiseOnUpdateFeedsStarted(force_download);

				// The "CopyTo()" construct prevents against InvalidOpExceptions/ArgumentOutOfRange
				// exceptions and keep the loop alive if FeedsTable gets modified from other thread(s)
				string[] keys;
			
				lock (FeedsTable.SyncRoot) {
					keys = new string[FeedsTable.Count];
					if (FeedsTable.Count > 0)
						FeedsTable.Keys.CopyTo(keys, 0);	
				}

				//foreach(string sKey in FeedsTable.Keys){
				//  feedsFeed current = FeedsTable[sKey];	

				for(int i = 0, len = keys.Length; i < len; i++){

					feedsFeed current = FeedsTable[keys[i]];	
				
					if (current == null)	// may have been redirected/removed meanwhile
						continue;

					try{ 

						// new: giving up after three unsuccessfull requests
						if (!force_download && current.causedExceptionCount >= 3) {
							continue;
						}

						if(current.refreshrateSpecified && (current.refreshrate == 0)){
							continue; 	    
						}

						if(itemsTable.ContainsKey(current.link)){ //check if feed downloaded in the past
					
							//check if enough time has elapsed as to require a download attempt
							if((!force_download) && current.lastretrievedSpecified){
		
								double timeSinceLastDownload = DateTime.Now.Subtract(current.lastretrieved).TotalMilliseconds;
								int refreshRate           =  current.refreshrateSpecified ?  current.refreshrate : this.RefreshRate; 
		
								if(timeSinceLastDownload < refreshRate){
									continue; //no need to download 
								}	 																		
							}//if(current.lastretrievedSpecified...) 

	      
							if (current.category != null && IsChildOrSameCategory(category, current.category)) {
								if (this.AsyncGetItemsForFeed(current.link, true, false))
									anyRequestQueued = true;
							}
	    
						}else{

							if (current.category != null && IsChildOrSameCategory(category, current.category)) {
								if (this.AsyncGetItemsForFeed(current.link, force_download, false))
									anyRequestQueued = true;
							}
						}

						Thread.Sleep(15);	// force a context switches

					}catch(Exception e){ 

						Trace("RefreshFeeds(string,bool) unexpected error processing feed '{0}': {1}", current.link, e.ToString()); 
	  
					}	  									

				}//for(i)

			}catch(InvalidOperationException ioe){// New feeds added to FeedsTable from another thread  
							
				Trace("RefreshFeeds(string,bool) InvalidOperationException: {0}", ioe.ToString()); 
				
			} 							
			finally {
				if (offline || !anyRequestQueued)
					RaiseOnAllAsyncRequestsCompleted();
			}
		}

		/// <summary>
		/// Determines whether two categories are the same or are whether 
		/// </summary>
		/// <param name="category">The category we are testing against</param>
		/// <param name="testCategory">The category being tested</param>
		/// <returns></returns>
		private static bool IsChildOrSameCategory(string category, string testCategory){
		
			if(testCategory.Equals(category) || testCategory.StartsWith(category + NewsHandler.CategorySeparator))
				return true; 
			else
				return false; 
		}
	  
		/// <summary>
		/// Converts the input XML document from OCS, OPML or SIAM to the RSS Bandit feed list 
		/// format. 
		/// </summary>
		/// <param name="doc">The input feed list</param>
		/// <returns>The converted feed list</returns>
		/// <exception cref="ApplicationException">if the feed list format is unknown</exception>
		public XmlDocument ConvertFeedList(XmlDocument doc){
					
			ImportFilter importFilter = new ImportFilter(doc);

			XslTransform transform = importFilter.GetImportXsl();

			if(transform != null) {
				// We have a format other than Bandit
				// Apply the import filter (transform)
				XmlDocument temp = new XmlDocument(); 
				temp.Load(transform.Transform(doc, null)); 
				doc = temp;
			}
			else {
				// see if we have a Bandit format
				if(importFilter.Format == ImportFeedFormat.Bandit) {
					// load and validate the Bandit feed file
					//validate document 
					XmlParserContext context = new XmlParserContext(null, new RssBanditXmlNamespaceResolver(), null, XmlSpace.None);
					XmlValidatingReader vr = new RssBanditXmlValidatingReader(doc.OuterXml, XmlNodeType.Document, context);
					vr.Schemas.Add(feedsSchema); 
					vr.ValidationType = ValidationType.Schema; 	
					vr.ValidationEventHandler += new ValidationEventHandler(ValidationCallbackOne);		
					doc.Load(vr); 
					vr.Close();
				}
				else {
					// We have an unknown format
					throw new ApplicationException("Unknown Feed Format.",null);
				}
			}
		
			return doc; 
		}




		/// <summary>
		/// Replaces the existing list of feeds used by the application with the list of 
		/// feeds in the specified XML document. The file must be an RSS Bandit feed list
		/// or a SIAM file. 
		/// </summary>
		/// <param name="feedlist">The list of feeds</param>
		/// <exception cref="ApplicationException">If the file is not a SIAM, OPML or RSS bandit feedlist</exception>		
		public void ReplaceFeedlist(Stream feedlist){
			this.ImportFeedlist(feedlist, String.Empty, true); 
		}



		/// <summary>
		/// Replaces or imports the existing list of feeds used by the application with the list of 
		/// feeds in the specified XML document. The file must be an RSS Bandit feed list
		/// or a SIAM file. 
		/// </summary>
		/// <param name="feedlist">The list of feeds</param>
		/// <param name="category">The category to import the feeds into</param>
		/// <param name="replace">Indicates whether the feedlist should be replaced or not</param>
		/// <exception cref="ApplicationException">If the file is not a SIAM, OPML or RSS bandit feedlist</exception>		
		public void ImportFeedlist(Stream feedlist, string category, bool replace){
		
			XmlDocument doc = new XmlDocument(); 			
			doc.Load(feedlist); 

			//convert feed list to RSS Bandit format
			doc = ConvertFeedList(doc); 

			//load up 
			XmlNodeReader reader = new XmlNodeReader(doc);		
			XmlSerializer serializer  = XmlHelper.SerializerCache.GetSerializer(typeof(feeds));
			feeds myFeeds = (feeds)serializer.Deserialize(reader); 
			reader.Close();

			bool keepLocalSettings = true; 
			this.ImportFeedlist(myFeeds, category, replace, keepLocalSettings); 
		}


		/// <summary>
		/// Replaces or imports the existing list of feeds used by the application with the list of 
		/// feeds in the specified XML document. The file must be an RSS Bandit feed list
		/// or a SIAM file. 
		/// </summary>
		/// <param name="myFeeds">The list of feeds</param>
		/// <param name="category">The category to import the feeds into</param>
		/// <param name="replace">Indicates whether the feedlist should be replaced or not</param>
		/// <param name="keepLocalSettings">Indicates that the local feed specific settings should not be overwritten 
		/// by the imported settings</param>
		/// <exception cref="ApplicationException">If the file is not a SIAM, OPML or RSS bandit feedlist</exception>		
		public void ImportFeedlist(feeds myFeeds, string category, bool replace, bool keepLocalSettings){	
		 
			
			//feedListImported = true; 
			/* TODO: Sync category settings */ 

			CategoriesCollection categories = new CategoriesCollection(); 
			FeedColumnLayoutCollection layouts = new FeedColumnLayoutCollection(); 

			FeedsCollection syncedfeeds = new FeedsCollection();						

			// InitialHTTPLastModifiedSettings used to reduce the initial payload
			// for the first request of imported feeds.
			// HTTP endpoints considering also/only the ETag header will influence 
			// if a 200 OK is returned onrequest or not.
			// HTTP endpoints not considering the Last Modified header will not be affected.
			DateTime[] dta = RssHelper.InitialLastRetrievedSettings(myFeeds.feed.Count, this.RefreshRate);
			int dtaCount = dta.Length, count = 0;

			while(myFeeds.feed.Count != 0){
				
				feedsFeed f1 = (feedsFeed) myFeeds.feed[0]; 

				bool isBadUri = false; 
				try{ new Uri(f1.link); }catch(Exception) { isBadUri = true;}

				if(isBadUri){
					myFeeds.feed.RemoveAt(0);
					continue;
				}

				if(replace && _feedsTable.ContainsKey(f1.link)){					

					//copy category information over
					feedsFeed f2 = _feedsTable[f1.link]; 

					if(!keepLocalSettings){ 

						f2.category = f1.category; 

						if((f2.category != null) && !categories.ContainsKey(f2.category)){
							categories.Add(f2.category); 
						}

						//copy listview layout information over
						if((f1.listviewlayout != null) && !layouts.ContainsKey(f1.listviewlayout)){
							listviewLayout layout = FindLayout(f1.listviewlayout, myFeeds.listviewLayouts);

							if(layout != null)
								layouts.Add(f1.listviewlayout, layout.FeedColumnLayout); 
							else
								f1.listviewlayout = null; 
						}
						f2.listviewlayout = (f1.listviewlayout != null ? f1.listviewlayout: f2.listviewlayout);


						//copy title information over 
						f2.title   = f1.title; 
					

						//copy various settings over			
						f2.markitemsreadonexitSpecified = f1.markitemsreadonexitSpecified;
						if(f1.markitemsreadonexitSpecified){
							f2.markitemsreadonexit = f1.markitemsreadonexit;
						}

						f2.stylesheet = (f1.stylesheet != null ? f1.stylesheet: f2.stylesheet);
						f2.maxitemage = (f1.maxitemage != null ? f1.maxitemage: f2.maxitemage);
						f2.alertEnabledSpecified = f1.alertEnabledSpecified;
						f2.alertEnabled = (f1.alertEnabledSpecified ? f1.alertEnabled : f2.alertEnabled);
						f2.refreshrateSpecified = f1.refreshrateSpecified;
						f2.refreshrate = (f1.refreshrateSpecified ? f1.refreshrate : f2.refreshrate);

						//DISCUSS
						//f2.downloadenclosures ?

						// save to sync.: key is generated the same on every machine, IV seems to have no influence 
						f2.authPassword = f1.authPassword; 
						f2.authUser     = f1.authUser; 

					}//if(!keepLocalSettings)

					//copy over deleted stories
					foreach(string story in f1.deletedstories){
						if(!f2.deletedstories.Contains(story)){
							f2.deletedstories.Add(story); 
						}		
					}//foreach

					//copy over read stories
					foreach(string story in f1.storiesrecentlyviewed){
						if(!f2.storiesrecentlyviewed.Contains(story)){
							f2.storiesrecentlyviewed.Add(story); 
						}		
					}//foreach					

					if(itemsTable.ContainsKey(f2.link)){
						ArrayList items = ((FeedInfo)itemsTable[f2.link]).itemsList;

						foreach(NewsItem item in items){
							if(f2.storiesrecentlyviewed.Contains(item.Id)){
								item.BeenRead = true; 
							}
						}					
					}

					syncedfeeds.Add(f2.link, f2); 
				
				}else{ 

					if(replace){
						if((f1.category != null) && !categories.ContainsKey(f1.category)){
							categories.Add(f1.category); 
						}

						if((f1.listviewlayout != null) && !layouts.ContainsKey(f1.listviewlayout)){
							listviewLayout layout = FindLayout(f1.listviewlayout, myFeeds.listviewLayouts);

							if(layout != null)
								layouts.Add(f1.listviewlayout, layout.FeedColumnLayout); 
							else
								f1.listviewlayout = null; 
						}
						
						if(!syncedfeeds.ContainsKey(f1.link)){
							syncedfeeds.Add(f1.link, f1); 			
						}		

					}else{						

						if(category.Length > 0){
							f1.category = (f1.category == null ? category : category + NewsHandler.CategorySeparator + f1.category);
						}
						//f1.category = (category  == String.Empty ? f1.category : category + NewsHandler.CategorySeparator + f1.category); 
						if(!_feedsTable.ContainsKey(f1.link)){
							f1.lastretrievedSpecified = true;
							f1.lastretrieved = dta[count % dtaCount];
							_feedsTable.Add(f1.link, f1); 	
						}					
					}
				}
			
				myFeeds.feed.RemoveAt(0); 
				count++;
			}


			ListDictionary serverList = new ListDictionary(); 
			ListDictionary identityList = new ListDictionary(); 
			
			/* copy over user identity information */
			foreach(UserIdentity identity in myFeeds.identities){

				if(replace){
					identityList.Add(identity.Name, identity); 
				} else if(!this.identities.Contains(identity.Name)){								
					this.identities.Add(identity.Name, identity); 
				}
			}//foreach
			

			/* copy over newsgroup information */
			foreach(NntpServerDefinition server in myFeeds.nntpservers){
				if(replace){
					serverList.Add(server.Name, server); 
				} else if(!this.identities.Contains(server.Name)){								
					this.nntpServers.Add(server.Name, server); 
				}
			}

			// copy over layout information 
			foreach(listviewLayout layout in myFeeds.listviewLayouts){
				if(replace){
					if(layout.FeedColumnLayout.LayoutType == LayoutType.GlobalFeedLayout ||
					   layout.FeedColumnLayout.LayoutType == LayoutType.GlobalCategoryLayout ||
					   layout.FeedColumnLayout.LayoutType == LayoutType.SearchFolderLayout ||
					   layout.FeedColumnLayout.LayoutType == LayoutType.SpecialFeedsLayout)
					layouts.Add(layout.ID, layout.FeedColumnLayout); 

				} else if(!this.layouts.ContainsKey(layout.ID)){ //don't replace layouts on import
					if(layout.FeedColumnLayout.LayoutType != LayoutType.GlobalFeedLayout ||
						layout.FeedColumnLayout.LayoutType != LayoutType.GlobalCategoryLayout ||
						layout.FeedColumnLayout.LayoutType != LayoutType.SearchFolderLayout ||
						layout.FeedColumnLayout.LayoutType != LayoutType.SpecialFeedsLayout)		
					this.layouts.Add(layout.ID, layout.FeedColumnLayout); 
				}
			}


			if(replace){
				
				/* update feeds table */ 
				this._feedsTable = syncedfeeds; 
				/* update category information */
				this.categories = categories; 
				/* update identities */ 
				this.identities = identityList; 
				/* update servers */
				this.nntpServers = serverList; 
				/* update layouts */
				this.layouts = layouts;
			
			}else{
			
				if(myFeeds.categories.Count == 0){ //no new subcategories
					if(category.Length > 0 && this.categories.ContainsKey(category) == false){
						this.categories.Add(category); 
					}	  
				}else {

					foreach(category cat in myFeeds.categories){
						string cat2 = (category.Length == 0 ? cat.Value : category + NewsHandler.CategorySeparator + cat.Value); 
				
						if(this.categories.ContainsKey(cat2) == false){
							this.categories.Add(cat2); 
						}
					}
				}
			}

			//if original feed list was invalid then reset error indication	
			if(validationErrorOccured){
				validationErrorOccured = false; 
			}
			
		}


		/// <summary>
		/// Merges the list of feeds in the specified XML document with that currently 
		/// used by the application. The file can either be an RSS Bandit feed list or an 
		/// OPML file. 
		/// </summary>
		/// <param name="feedlist">The list of feeds</param>
		/// <exception cref="ApplicationException">If the file is neither an OPML file or RSS bandit feedlist</exception>		
		public void ImportFeedlist(Stream feedlist){
			this.ImportFeedlist(feedlist, String.Empty, false); 
		}
	  
    

		/// <summary>
		/// Merges the list of feeds in the specified XML document with that currently 
		/// used by the application. The file can either be an RSS Bandit feed list or an 
		/// OPML file. 
		/// </summary>
		/// <param name="feedlist">The list of feeds</param>
		/// <param name="category">The category to import the feeds into</param>
		/// <exception cref="ApplicationException">If the file is neither an OPML file or RSS bandit feedlist</exception>		
		public void ImportFeedlist(Stream feedlist, string category){
		
			try{ 
				this.ImportFeedlist(feedlist, category, false); 

				/* XmlDocument doc = new XmlDocument(); 
				//XmlDocument fl = new XmlDocument(); 
				doc.Load(feedlist); 

				//convert feed list to RSS Bandit format
				doc = ConvertFeedList(doc); 

				//load up 
				XmlNodeReader reader = new XmlNodeReader(doc);		
				XmlSerializer serializer  = new XmlSerializer(typeof(feeds));
				feeds myFeeds = (feeds)serializer.Deserialize(reader); 
				reader.Close(); 

				if(_feedsTable == null){	
					_feedsTable = new FeedsCollection(); 
				}
		 
			 

				foreach(feedsFeed f in myFeeds.feed){
		
					//if the same feed seen twice, ignore second occurence 
					if(_feedsTable.ContainsKey(f.link) == false){
						if(category != String.Empty){
							f.category = (f.category == null ? category : category + NewsHandler.CategorySeparator + f.category);
						}
						//f.category = (category  == String.Empty ? f.category : category + NewsHandler.CategorySeparator + f.category); 
						_feedsTable.Add(f.link, f); 
					}
				}		
	
				if(myFeeds.categories.Count == 0){ //no new subcategories
					if(category != String.Empty && this.categories.ContainsKey(category) == false){
						this.categories.Add(category); 
					}	  
				}else {

					foreach(string cat in myFeeds.categories){
						string cat2 = (category == String.Empty ? cat : category + NewsHandler.CategorySeparator + cat); 
				
						if(this.categories.ContainsKey(cat2) == false){
							this.categories.Add(cat2); 
						}
					}
				}
				//if original feed list was invalid then reset error indication	
				if(validationErrorOccured){
					validationErrorOccured = false; 
				}*/				

			}catch(Exception e){
				throw new ApplicationException(e.Message, e); 
			}

		}

		/// <summary>
		/// Handles errors that occur during schema validation of RSS feed list
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		public static void ValidationCallbackOne(object sender,
			ValidationEventArgs args) {
			if(args.Severity == XmlSeverityType.Error){
				Trace("ValidationCallbackOne() message: {0}", args.Message);
			
				/* In some cases we corrupt feedlist.xml by not putting all referenced
				 * categories in <category> elements. This is not a fatal error. 
				 * 
				 * Also we sometimes corrupt subscriptions.xml by putting multiple entries for the same category.
				 */
				XmlSchemaException xse = args.Exception;
				if(xse != null){
					Type xseType            = xse.GetType(); 
					FieldInfo resFieldInfo  = xseType.GetField("res", BindingFlags.NonPublic | BindingFlags.Instance);             

					string errorType = (string) resFieldInfo.GetValue(xse); 

					if(!errorType.Equals("Sch_UnresolvedKeyref") && !errorType.Equals("Sch_DuplicateKey")){								
						validationErrorOccured = true; 		
					} else{
						categoryMismatch = true; 
					}
				}//if(xse != null) 
			}//if(args.Severity...)	

		}


		/// <summary>
		/// Saves a particular RSS feed.
		/// </summary>
		/// <remarks>This method should be thread-safe</remarks>
		/// <param name="feed">The the feed to save. This is an identifier
		/// and not used to actually fetch the feed from the WWW.</param>
		/// <returns>An identifier for the saved feed. </returns>		
		private string SaveFeed(feedsFeed feed){

			TimeSpan maxItemAge    = this.GetMaxItemAge(feed.link); 
			FeedDetailsInternal fi = (FeedDetailsInternal) this.itemsTable[feed.link];
			ArrayList items        = fi.ItemsList;

			/* remove items that have expired according to users cache requirements */ 
			if(maxItemAge != TimeSpan.MinValue){ /* check if feed set to never delete items */ 

				lock(items){
			
					for(int i = 0, count = items.Count ; i < count ; i++){
						NewsItem item = (NewsItem) items[i]; 
				
						if(feed.deletedstories.Contains(item.Id) || ((DateTime.Now - item.Date) >= maxItemAge)){
						
							items.Remove(item); 
							NewsHandler.RelationCosmosRemove(item);
							SearchHandler.IndexRemove(item);
							count--; 
							i--; 
						}//if
					}//for
				}//lock
			}//if(maxItemAge != TimeSpan.MinValue)						


			return this.CacheHandler.SaveFeed(fi); 
		}

		/// <summary>
		/// Returns an RSS feed. 
		/// </summary>
		/// <param name="feed">The feed whose FeedInfo is required.</param>
		/// <returns>The requested feed or null if it doesn't exist</returns>
		private FeedDetailsInternal GetFeed(feedsFeed feed){
			
			FeedDetailsInternal fi = this.CacheHandler.GetFeed(feed); 

			if(fi != null){
				
				/* remove items that have expired according to users cache requirements */ 
				TimeSpan maxItemAge = this.GetMaxItemAge(feed.link);
			  
				int readItems = 0; 

				ArrayList items = fi.ItemsList;
				lock(items){
			    
					/* check if feed set to never delete items */ 
					bool keepAll = (maxItemAge == TimeSpan.MinValue) && (feed.deletedstories.Count == 0); 

					//since we are going to use this value for calculation we should change it 
					//from TimeSpan.MinValue which is used to indicate 'keep indefinitely' to TimeSpan.MaxValue
					maxItemAge = (maxItemAge == TimeSpan.MinValue ? TimeSpan.MaxValue: maxItemAge);

					for(int i = 0, count = items.Count ; i < count ; i++){
						NewsItem item = (NewsItem) items[i]; 
			      
						if((!keepAll) && ((DateTime.Now - item.Date) >= maxItemAge) || feed.deletedstories.Contains(item.Id)){
				
							//items.Remove(item);  // calls internal IndexOf() and RemoveAt()	
							items.RemoveAt(i); 
							NewsHandler.RelationCosmosRemove(item);							
							i--; 
							count--; 
			      
						}else if(item.BeenRead){							
							readItems++;
						}
			    
					}			  
				}

				if(readItems == items.Count){
					feed.containsNewMessages = false; 
				}else{
					feed.containsNewMessages = true; 
				}	
			
			}//if(fi != null)

			return fi; 
		}

		/// <summary>
		/// Merge and purge items.
		/// </summary>
		/// <param name="oldItems">IList with the old items</param>
		/// <param name="newItems">IList with the new items</param>
		/// <param name="deletedItems">IList with the IDs of deleted items</param>
		/// <param name="receivedNewItems">IList with the really new (received) items.</param>
		/// <param name="onlyKeepNewItems">Indicates that we only want the items from newItems to be kept. If this value is true 
		/// then this method merely copies over item state of any oldItems that are in newItems then returns newItems</param>
		/// <returns>IList merge/purge result</returns>
		public static IList MergeAndPurgeItems(IList oldItems, IList newItems, IList deletedItems, out IList receivedNewItems, bool onlyKeepNewItems) {
			receivedNewItems = new ArrayList();
			//ArrayList removedOldItems = new ArrayList(); 

			lock(oldItems){

				foreach(NewsItem newitem in newItems){
					int index = oldItems.IndexOf(newitem);
					if(index == -1) {
						if(!deletedItems.Contains(newitem.Id)) {
							receivedNewItems.Add(newitem);
							oldItems.Add(newitem);   
							//perform whatever processing is needed
							NewsHandler.ReceivingNewsChannelServices.ProcessItem(newitem);
						}

					}else{
						NewsItem olditem   = (NewsItem) oldItems[index];										    
						newitem.BeenRead   = olditem.BeenRead;
						/*
						COMMENTED OUT BECAUSE WE WON'T SAVE NEWLY DOWNLOADED TEXT IF THE 
						FEED IS UPDATED WITH THE CODE BELOW. 
						
						//We don't need strings in memory if we've read it. However we have to 
						//account for the edge case where the feed list was imported and this was 
						//read but hasn't yet been saved to the cache. 
						//
						if(!feedListImported && newitem.BeenRead){ 
							newitem.SetContent((string) null, newitem.ContentType); 
						} */ 
						newitem.Date       = olditem.Date; //so the date is from when it was first fetched
						newitem.FlagStatus = olditem.FlagStatus; 						

						if(olditem.WatchComments){
							newitem.WatchComments = true; 
							
							if((olditem.HasNewComments) || (olditem.CommentCount < newitem.CommentCount)){
								newitem.HasNewComments = true; 								
							}

						}//if(olditem.WatchComments) 
						
						//feed doesn't support <slash:comments>, so we use the existing comment count 
						//in case we previously obtained it by fetching the CommentRssUrl
						if(newitem.CommentCount == NewsItem.NoComments){
							newitem.CommentCount = olditem.CommentCount; 
						}

						//see if we've downloaded any of the enclosures on the old item
						if(olditem.Enclosures.Count > 0){
							foreach(Enclosure enc in olditem.Enclosures){
								int j = newitem.Enclosures.IndexOf(enc);
								
								if(j != -1){
									Enclosure oldEnc = (Enclosure) newitem.Enclosures[j];
									enc.Downloaded   = oldEnc.Downloaded;
								}else{
									newitem.Enclosures.Add(enc);
								}
							}
						}

						oldItems.RemoveAt(index);
						oldItems.Add(newitem);
						//	removedOldItems.Add(olditem); 
					}
				}//foreach
				
				//remove old objects from relation cosmos and add newly downloaded items to relationcosmos
				//NewsHandler.RelationCosmosRemoveRange(removedOldItems); 
				NewsHandler.RelationCosmosAddRange(receivedNewItems);
				
			}//lock

			if(onlyKeepNewItems){
				return newItems; 
			}else{
				return oldItems;
			}

		}

		/// <summary>
		/// Posts a comment in reply to an item using either NNTP or the CommentAPI 
		/// </summary>
		/// <param name="url">The URL to post the comment to</param>
		/// <param name="item2post">An RSS item that will be posted to the website</param>
		/// <param name="inReply2item">An RSS item that is the post parent</param>		
		/// <exception cref="WebException">If an error occurs when the POSTing the 
		/// comment</exception>
		public void PostComment(string url, NewsItem item2post, NewsItem inReply2item){			  
			
			if(inReply2item.CommentStyle == SupportedCommentStyle.CommentAPI){
				this.RssParser.PostCommentViaCommentAPI(url, item2post, inReply2item, GetFeedCredentials(inReply2item.Feed));
			}else if(inReply2item.CommentStyle == SupportedCommentStyle.NNTP){
				NntpParser.PostCommentViaNntp(item2post, inReply2item, GetNntpServerCredentials(inReply2item.Feed));
			}

		}
		
		/// <summary>
		/// Posts a new item to a feed (currently only NNTP feeds) 
		/// </summary>
		/// <remarks>How about Atom feed posting?</remarks>
		/// <param name="item2post">An RSS item that will be posted to the website/NNTP Group</param>
		/// <param name="postTarget">An feedsFeed as the post target</param>		
		/// <exception cref="WebException">If an error occurs when the POSTing the 
		/// comment</exception>
		public void PostComment(NewsItem item2post, feedsFeed postTarget){			  
			
			if(item2post.CommentStyle == SupportedCommentStyle.NNTP){
				NntpParser.PostCommentViaNntp(item2post, postTarget, GetNntpServerCredentials(postTarget));
			}
		}

		#region RelationCosmos management
		/// <summary>
		/// </summary>
		/// <param name="item"></param>
		/// <param name="excludeItemsList"></param>
		/// <returns></returns>
		public ICollection GetItemsWithIncomingLinks(NewsItem item, IList excludeItemsList){
			if(NewsHandler.buildRelationCosmos)
				return relationCosmos.GetIncoming(item, excludeItemsList);
			else 
				return new ArrayList(); 
		}
		
		/// <summary>
		/// </summary>
		/// <param name="item"></param>
		/// <param name="excludeItemsList"></param>
		/// <returns></returns>
		public ICollection GetItemsFromOutGoingLinks(NewsItem item, IList excludeItemsList){
			if(NewsHandler.buildRelationCosmos)
				return relationCosmos.GetOutgoing(item, excludeItemsList);
			else 
				return new ArrayList(); 			
		}
	 
		/// <summary>
		/// </summary>
		/// <param name="item"></param>
		/// <param name="excludeItemsList"></param>
		/// <returns></returns>
		public bool HasItemAnyRelations(NewsItem item, IList excludeItemsList) {
			if(NewsHandler.buildRelationCosmos)
				return relationCosmos.HasIncomingOrOutgoing(item, excludeItemsList);
			else
				return false; 
		}

		/// <summary>
		/// Internal used accessor
		/// </summary>
		/// <param name="relation"></param>
		internal static void RelationCosmosAdd (RelationBase relation) {			
			if(NewsHandler.buildRelationCosmos)
				relationCosmos.Add(relation);
			else 
				return;
		}
		internal static void RelationCosmosAddRange (IList relations) {
			if(NewsHandler.buildRelationCosmos)
				relationCosmos.AddRange(relations);
			else 
				return;
		}
		internal static void RelationCosmosRemove (RelationBase relation) {
			if(NewsHandler.buildRelationCosmos)
				relationCosmos.Remove(relation);
			else 
				return;
		}
		internal static void RelationCosmosRemoveRange (IList relations) {
			if(NewsHandler.buildRelationCosmos)
				relationCosmos.RemoveRange(relations);
			else 
				return;
		}
		#endregion

		#region ReceivingNewsChannel Manangement		
		/// <summary>
		/// Register INewsChannel processing services 
		/// </summary>
		public void RegisterReceivingNewsChannel (INewsChannel channel) {
			// We use an instance method to register services.
			// So we are able to change later the internal processing to a non-static
			// class/instance if required.
			receivingNewsChannel.RegisterNewsChannel(channel);
		}
		/// <summary>
		/// Unregister INewsChannel processing services 
		/// </summary>
		public void UnregisterReceivingNewsChannel (INewsChannel channel) {
			// We use an instance method to register services.
			// So we are able to change later the internal processing to a non-static
			// class/instance if required.
			receivingNewsChannel.UnregisterNewsChannel(channel);
		}

		/// <summary>
		/// Gets the receiving news channel.
		/// </summary>
		/// <value>The receiving news channel services.</value>
		internal static NewsChannelServices ReceivingNewsChannelServices {
			get { return receivingNewsChannel; }
		}

		#endregion

	}


	#region NewsFeedProperty enum

	/// <summary>
	/// Defines all storage relevant feedsFeed properties. On any change
	/// of a feedsFeed property, that feed requires to be saved with the
	/// subscriptions list, to the cache or re-indexed!
	/// </summary>
	[Flags]
	public enum NewsFeedProperty {
		None = 0,
		/// <summary>Requires subscriptions update/save, re-index</summary>
		FeedLink = 0x1,
		/// <summary>Requires re-index</summary>
		FeedUrl = 0x2,
		/// <summary>Requires subscriptions update/save, re-index</summary>
		FeedTitle = 0x4,
		/// <summary>Requires subscriptions update/save, re-index</summary>
		FeedCategory = 0x8,
		/// <summary>Requires re-index</summary>
		FeedDescription = 0x10,
		/// <summary>Requires cache update/save, re-index</summary>
		FeedType = 0x20,
		/// <summary>Requires subscriptions update/save, re-index</summary>
		FeedItemsDeleteUndelete = 0x40,
		/// <summary>Requires cache update/save</summary>
		FeedItemFlag = 0x80,	
		/// <summary>Requires subscriptions and cache update/save</summary>
		FeedItemReadState	= 0x100,
		/// <summary>Requires cache update/save</summary>
		FeedItemCommentCount = 0x200,
		/// <summary>Requires subscriptions update/save</summary>
		FeedMaxItemAge		= 0x400,
		/// <summary>Requires cache update/save</summary>
		FeedItemWatchComments = 0x800,
		/// <summary>Requires subscriptions update/save</summary>
		FeedRefreshRate		= 0x1000,
		/// <summary>Requires subscriptions update/save</summary>
		FeedCacheUrl		= 0x2000,
		/// <summary>Requires subscriptions update/save</summary>
		FeedAdded			= 0x4000,
		/// <summary>Requires subscriptions update/save</summary>
		FeedRemoved			= 0x8000,
		/// <summary>Requires subscriptions update/save</summary>
		FeedCategoryRemoved	= 0x10000,
		/// <summary>Requires subscriptions update/save</summary>
		FeedCategoryAdded	= 0x20000,
		/// <summary>Requires cache update/save </summary>
		FeedCredentials		= 0x40000,
		/// <summary>Requires subscriptions update/save </summary>
		FeedAlertOnNewItemsReceived = 0x80000,
		/// <summary>Requires subscriptions update/save </summary>
		FeedMarkItemsReadOnExit = 0x100000,
		/// <summary>Requires subscriptions update/save </summary>
		FeedStylesheet		= 0x200000,
		/// <summary>Requires cache update/save</summary>
		FeedItemNewCommentsRead = 0x400000,
		/// <summary> General change, requires subscriptions update/save</summary>
		General = 0x8000000,
	}

	//	/// <summary>
	//	/// Defines all index relevant NewsItem properties, 
	//	/// that are part of the lucene search index. On any change
	//	/// of a NewsItem property, that NewsItem requires to be re-indexed!
	//	/// </summary>
	//	public enum NewsItemProperty {
	//		ItemAuthor,
	//		ItemTitle,
	//		ItemLink,
	//		ItemDate,
	//		ItemTopic,
	//		Other,
	//	}

	#endregion

	/// <summary>
	/// Interface represents extended information about a particular feed
	/// (internal use only)
	/// </summary>
	internal interface FeedDetailsInternal: IFeedDetails {
		ArrayList ItemsList { get; set; }
		string FeedLocation {get; set; }
		string Id {get; set; }
		void WriteTo(XmlWriter writer);
		void WriteTo(XmlWriter writer, bool noDescriptions); 
		void WriteItemContents(BinaryReader reader, BinaryWriter writer); 
	}
  
	/// <summary>
	/// Get informations about the size of an object or item
	/// </summary>
	public interface ISizeInfo {
		int GetSize();
		string GetSizeDetails();
	}

	#region RssBanditXmlNamespaceResolver 

	/// <summary>
	/// Helper class used for treating v1.2.* RSS Bandit feedlist.xml files as RSS Bandit v1.3.* 
	/// subscriptions.xml files
	/// </summary>
	internal class RssBanditXmlNamespaceResolver : XmlNamespaceManager {

		public RssBanditXmlNamespaceResolver(): base(new NameTable()){}

		public override void AddNamespace(string prefix, string uri) {   
			if ( uri == NamespaceCore.Feeds_v2003 ) {	
				uri = NamespaceCore.Feeds_vCurrent;	
			}      			
			base.AddNamespace(prefix, uri);      
		}

	}

	#endregion

	#region RssBanditXmlValidatingReader 

	/// <summary>
	/// Helper class used for treating v1.2.* RSS Bandit feedlist.xml files as RSS Bandit v1.3.* 
	/// subscriptions.xml files
	/// </summary>
	internal class RssBanditXmlValidatingReader: XmlValidatingReader{	

		public RssBanditXmlValidatingReader(Stream s, XmlNodeType nodeType, XmlParserContext context): base(s, nodeType, context){}
		public RssBanditXmlValidatingReader(string s, XmlNodeType nodeType, XmlParserContext context): base(s, nodeType, context){}

		public override string Value{
			get {
				if((this.NodeType == XmlNodeType.Attribute) && 
					(base.Value == NamespaceCore.Feeds_v2003)){
					return NamespaceCore.Feeds_vCurrent; 
				}else{
					return base.Value; 
				}
			}
		}
	}//class 

	#endregion 

}

#region CVS Version Log
/*
 * $Log: NewsHandler.cs,v $
 * Revision 1.188  2007/09/17 22:09:51  carnage4life
 * Added features to support exporting folder hierarchy to NewsGator
 *
 * Revision 1.187  2007/09/16 15:24:51  carnage4life
 * Fixed compile error introduced by last checkin
 *
 * Revision 1.185  2007/08/09 17:47:02  t_rendelmann
 * changed: disabled finnish stemmer because of exceptions; added comment
 *
 * Revision 1.184  2007/07/26 02:50:56  carnage4life
 * Fixed issue where global refresh rate is not applied to newly subscribed feeds
 *
 * Revision 1.183  2007/07/21 12:26:21  t_rendelmann
 * added support for "portable Bandit" version
 *
 * Revision 1.182  2007/07/11 18:11:50  carnage4life
 * Fixed ArgumentNullException if we couldn't detect the file extension of a favicon
 *
 * Revision 1.181  2007/07/07 10:32:20  t_rendelmann
 * fix: report search errors to the user
 *
 * Revision 1.180  2007/06/09 18:32:47  carnage4life
 * No results displayed when performing Web searches with Feedster or other search engines that return results as RSS feeds
 *
 * Revision 1.179  2007/06/07 19:51:22  carnage4life
 * Added full support for pagination in newspaper views
 *
 * Revision 1.178  2007/06/07 19:00:41  carnage4life
 * Added support for favicons in JPG format.
 *
 * Revision 1.177  2007/05/12 18:15:18  carnage4life
 * Changed a number of APIs to treat feed URLs as System.String instead of System.Uri because some feed URLs such as those containing unicode cannot be used to create instances of System.Uri
 *
 * Revision 1.176  2007/03/27 14:22:57  t_rendelmann
 * fixed: image magic length not correctly initialized
 *
 * Revision 1.175  2007/03/19 11:29:05  t_rendelmann
 * fixed: ignore invalid bitmap formats (mostly HTML failure reports or not common image format)
 *
 * Revision 1.174  2007/03/19 10:40:59  t_rendelmann
 * changed: now detecting the favicon image format of some common image formats (ico, png, gif)
 *
 * Revision 1.173  2007/03/13 16:13:13  t_rendelmann
 * fixed: sometimes the feed did not get anymore refreshed automatically
 *
 * Revision 1.172  2007/03/11 16:17:33  t_rendelmann
 * fixed: [ 1678119 ] Bandit sends out weird User Agent string (https://sourceforge.net/tracker/?func=detail&atid=615248&aid=1678119&group_id=96589)
 *
 * Revision 1.171  2007/03/04 03:25:05  carnage4life
 * Fixed issue where OPML exported by RSS Bandit cannot be read by IE 7
 *
 * Revision 1.170  2007/02/17 14:45:52  t_rendelmann
 * switched: Resource.Manager indexer usage to strongly typed resources (StringResourceTool)
 *
 * Revision 1.169  2007/02/15 16:37:48  t_rendelmann
 * changed: persisted searches now return full item texts;
 * fixed: we do now show the error of not supported search kinds to the user;
 *
 * Revision 1.168  2007/02/11 15:58:53  carnage4life
 * 1.) Added proper handling for when a podcast download exceeds the size limit on the podcast folder
 *
 * Revision 1.167  2007/02/10 15:28:22  carnage4life
 * Fixed issue where marking an item as read in a search folder doesn't mark it as read in the main feed.
 *
 * Revision 1.166  2007/02/07 17:58:36  t_rendelmann
 * fixed: null ref. exceptions caused on feeds without item content
 *
 * Revision 1.165  2007/02/01 16:00:41  t_rendelmann
 * fixed: option "Initiate download feeds at startup" was not taken over to the Options UI checkbox
 * fixed: Deserialization issue with Preferences types of wrong AppServices assembly version
 * fixed: OnPreferencesChanged() event was not executed at the main thread
 * changed: prevent execptions while deserialize DownloadTask
 *
 * Revision 1.164  2007/01/25 18:42:56  carnage4life
 * Fixed issues where setting to download last X enclosures from a feed were ignored
 *
 * Revision 1.163  2007/01/22 16:42:09  carnage4life
 * Changes to fix issues before shipping Jubilee release candidate
 *
 * Revision 1.162  2007/01/20 15:54:08  carnage4life
 * Fixed problems that occur when users import OPMLs with bad feed URLs
 *
 * Revision 1.161  2007/01/18 04:03:08  carnage4life
 * Completed support for custom newspaper view for search results
 *
 * Revision 1.160  2007/01/17 19:26:37  carnage4life
 * Added initial support for custom newspaper view for search results
 *
 * Revision 1.159  2007/01/16 15:31:27  t_rendelmann
 * changed: read state interpretation in returned search hits
 *
 * Revision 1.158  2007/01/16 00:27:54  carnage4life
 * Made some perf improvements related to SearchNewsItems()
 *
 * Revision 1.157  2007/01/14 19:30:46  t_rendelmann
 * cont. SearchPanel: first main form integration and search working (scope/populate search scope tree is still a TODO)
 *
 * Revision 1.156  2007/01/14 00:57:46  carnage4life
 * Added methods for finding NewsItems given feed URL and NewsItem.Id
 *
 * Revision 1.155  2006/12/20 17:05:05  carnage4life
 * RefreshFavicons now respects offline status
 *
 * Revision 1.154  2006/12/19 04:39:51  carnage4life
 * Made changes to AsyncRequest and RequestThread to become instance based instead of static
 *
 * Revision 1.153  2006/12/18 02:00:20  carnage4life
 * Added support for using Content-Disposition header to specify local file name for an enclosure
 *
 * Revision 1.152  2006/12/16 23:15:51  carnage4life
 * Fixed issue where comment feeds get confused when a comment is deleted from the feed,
 *
 * Revision 1.151  2006/12/16 22:26:51  carnage4life
 * Added CopyItemTo method that copies a NewsItem to a specific feedsFeed and does the logic to load item content from disk if needed
 *
 * Revision 1.150  2006/12/09 22:57:03  carnage4life
 * Added support for specifying how many podcasts downloaded from new feeds
 *
 * Revision 1.149  2006/12/07 13:17:18  t_rendelmann
 * now Lucene.OptimizeIndex() calls are only at startup and triggered by index folder modification datetime
 *
 * Revision 1.148  2006/12/05 04:06:25  carnage4life
 * Made changes so that when comments for an item are viewed from Watched Items folder, the actual feed is updated and vice versa
 *
 * Revision 1.147  2006/11/24 20:04:33  carnage4life
 * Made some changes to handle duplicate categories corrupting subscriptions.xml
 *
 * Revision 1.146  2006/11/23 16:02:03  t_rendelmann
 * moved to latest stable log4net
 *
 * Revision 1.145  2006/11/22 00:14:03  carnage4life
 * Added support for last of Podcast options
 *
 * Revision 1.144  2006/11/21 17:25:53  carnage4life
 * Made changes to support options for Podcasts
 *
 * Revision 1.143  2006/11/20 22:26:20  carnage4life
 * Added support for most of the Podcast and Attachment options except for podcast file extensions and copying podcasts to a specified folder
 *
 * Revision 1.142  2006/11/19 03:11:10  carnage4life
 * Added support for persisting podcast settings when changed in the Preferences dialog
 *
 * Revision 1.141  2006/11/12 01:25:01  carnage4life
 * 1.) Added Support for Alert windows on received podcasts.
 * 2.) Fixed feed mixup issues
 *
 * Revision 1.140  2006/10/30 19:18:24  carnage4life
 * Added code to automatically download enclosures from newly received items if the user selects that option
 *
 * Revision 1.139  2006/10/28 23:10:00  carnage4life
 * Added "Attachments/Podcasts" to Feed Properties and Category properties dialogs.
 *
 * Revision 1.138  2006/10/28 16:33:11  t_rendelmann
 * more progress to lucene search impl.
 *
 * Revision 1.137  2006/10/24 15:15:13  carnage4life
 * Changed the default folders for podcasts
 *
 * Revision 1.136  2006/10/21 23:34:15  carnage4life
 * Changes related to adding the "Download Attachment" right-click menu option in the list view
 *
 * Revision 1.135  2006/10/18 00:19:50  carnage4life
 * Fixed NullReferenceException in MakeAtomItem
 *
 * Revision 1.134  2006/10/17 15:23:26  carnage4life
 * Integrated BITS code for downloading enclosures
 *
 * Revision 1.133  2006/10/13 18:20:51  carnage4life
 * Added some debug statements
 *
 * Revision 1.132  2006/10/11 19:37:27  carnage4life
 * Fixed a bug I introduced in RelationCosmos processing
 *
 * Revision 1.131  2006/10/11 02:56:38  carnage4life
 * 1.) Fixed issue where we assumed that thr:replies would always point to an atom feed
 * 2.) Fixed issue where items marked as unread were showing up as read on restart
 *
 * Revision 1.130  2006/10/10 15:03:10  carnage4life
 * Merged differences between local machine and CVS versions
 *
 * Revision 1.129  2006/10/05 17:39:14  carnage4life
 * Fixed issue where favicons not loaded on startup
 *
 * Revision 1.128  2006/10/05 15:46:29  t_rendelmann
 * rework: now using XmlSerializerCache everywhere to get the XmlSerializer instance
 *
 * Revision 1.127  2006/10/05 08:00:13  t_rendelmann
 * refactored: use string constants for our XML namespaces
 *
 * Revision 1.126  2006/10/04 21:27:27  carnage4life
 * Fixed issue where relative links in Atom feeds did not work
 *
 * Revision 1.125  2006/10/03 16:52:21  t_rendelmann
 * cont. integrate lucene - the search part
 *
 * Revision 1.124  2006/10/03 14:03:26  carnage4life
 * Fixed issue where some favicons were not fetched
 *
 * Revision 1.123  2006/10/03 13:44:27  carnage4life
 * Fixed issue where outgoing/incoming links point to unread items when the item has been read
 *
 * Revision 1.122  2006/10/03 07:54:38  t_rendelmann
 * refresh to lucene index: optimized the way we add newly received items to prevent re-loading all the item content again for yet indexed items; also purged items (MaxItemAge expires) now handled the better way
 *
 * Revision 1.121  2006/09/29 18:11:59  t_rendelmann
 * a) integrated lucene index refreshs;
 * b) now using a centralized defined category separator;
 * c) unified decision about storage relevant changes to feed, feed and feeditem properties;
 *
 * Revision 1.120  2006/09/22 18:24:52  carnage4life
 * Fixed double click behavior on Outlook 2003 list view
 *
 * Revision 1.119  2006/09/11 21:32:50  carnage4life
 * Fixed issue where formerly watched comments lose their comment count when the feed is refetched and no comment count is in the feed
 *
 * Revision 1.118  2006/09/08 01:05:08  carnage4life
 * Fixed yet another bug in how comment feeds and comment watching work.
 *
 * Revision 1.117  2006/09/07 17:58:54  carnage4life
 * Fixed issue where comment counts where lost on watched comments whose feeds don't expose comment count
 *
 * Revision 1.116  2006/09/05 05:26:26  carnage4life
 * Fixed a number of bugs in comment watching code for comment feeds
 *
 * Revision 1.115  2006/09/03 19:08:50  carnage4life
 * Added support for favicons
 *
 * Revision 1.114  2006/09/02 02:11:46  carnage4life
 * Began favicon support
 *
 * Revision 1.113  2006/09/01 02:40:08  carnage4life
 * When "Update Category" is selected it refreshes all categories that begin with the name of the currently selected category instead of just that category and its child categories.
 *
 * Revision 1.112  2006/08/31 22:10:33  carnage4life
 * We now always send If-Last-Modified and default it to being the last retrieved date
 *
 * Revision 1.111  2006/08/18 19:10:57  t_rendelmann
 * added an "id" XML attribute to the feedsFeed. We need it to make the feed items (feeditem.id + feed.id) unique to enable progressive indexing (lucene)
 *
 */
#endregion
