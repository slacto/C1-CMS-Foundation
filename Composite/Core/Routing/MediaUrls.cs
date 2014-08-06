﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using Composite.Core.Extensions;
using Composite.Core.WebClient;
using Composite.Data;
using Composite.Data.Types;
using Composite.Core.IO;
using System.IO;

namespace Composite.Core.Routing
{
    /// <summary>    
    /// Responsible for parsing and building media urls
    /// </summary>
    public static class MediaUrls
    {
        internal static readonly string DefaultMediaStore = "MediaArchive";
        private static readonly string MediaUrl_UnprocessedInternalPrefix = "~/media(";
        private static readonly string MediaUrl_InternalPrefix = UrlUtils.PublicRootPath + "/media(";
        private static readonly string MediaUrl_PublicPrefix = UrlUtils.PublicRootPath + "/media/";

        private static readonly string MediaUrl_UnprocessedRenderPrefix = "~/Renderers/ShowMedia.ashx";
        private static readonly string MediaUrl_RenderPrefix = UrlUtils.PublicRootPath + "/Renderers/ShowMedia.ashx";

        private static readonly string ForbiddenUrlCharacters = @"<>*%&\?#""";


        /// <summary>
        /// Parses the URL.
        /// </summary>
        /// <param name="relativeUrl">The relative URL.</param>
        /// <returns></returns>
        public static MediaUrlData ParseUrl(string relativeUrl)
        {
            UrlKind urlKind;
            return ParseUrl(relativeUrl, out urlKind);
        }


        /// <summary>
        /// Parses the URL.
        /// </summary>
        /// <param name="relativeUrl">The relative URL.</param>
        /// <param name="urlKind">Kind of the URL.</param>
        /// <returns></returns>
        public static MediaUrlData ParseUrl(string relativeUrl, out UrlKind urlKind)
        {
            relativeUrl = relativeUrl.Replace("%28", "(").Replace("%29", ")");

            if(relativeUrl.StartsWith(MediaUrl_RenderPrefix, StringComparison.OrdinalIgnoreCase)
               || relativeUrl.StartsWith(MediaUrl_UnprocessedRenderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return ParseRenderUrl(relativeUrl, out urlKind);
            }

            urlKind = UrlKind.Undefined;

            bool isInternalLink = relativeUrl.StartsWith(MediaUrl_InternalPrefix, StringComparison.Ordinal);
            bool isInternalUnprocessedLink = !isInternalLink && relativeUrl.StartsWith(MediaUrl_UnprocessedInternalPrefix, StringComparison.Ordinal);

            if (isInternalLink || isInternalUnprocessedLink)
            {
                string prefix = isInternalLink ? MediaUrl_InternalPrefix : MediaUrl_UnprocessedInternalPrefix;

                var result = ParseInternalUrl(relativeUrl, prefix);
                if(result != null)
                {
                    urlKind = UrlKind.Internal;
                } 

                return result;
            }

            int minimumLengthOfPublicMediaUrl = MediaUrl_PublicPrefix.Length + 22; /* 2 - length of a compressed guid */
            if (relativeUrl.Length >= minimumLengthOfPublicMediaUrl
                && relativeUrl.StartsWith(MediaUrl_PublicPrefix))
            {
                // Parsing urls like /<site root>/media/{MediaId}*
                Guid mediaId;

                if (TryExtractMediaId(relativeUrl, MediaUrl_PublicPrefix.Length, out mediaId))
                {
                    NameValueCollection queryParams = new UrlBuilder(relativeUrl).GetQueryParameters();

                    urlKind = UrlKind.Public;
                    return new MediaUrlData
                    {
                        MediaId = mediaId,
                        MediaStore = DefaultMediaStore,
                        QueryParameters = queryParams
                    };
                }

                // Parsing urls like /<site root>/media/<MediaArchive>/{MediaId}*
                int slashOffset = relativeUrl.IndexOf('/', MediaUrl_PublicPrefix.Length + 1);

                if (slashOffset > MediaUrl_PublicPrefix.Length + 1 
                    && TryExtractMediaId(relativeUrl, slashOffset + 1, out mediaId))
                {
                    string mediaStore = relativeUrl.Substring(MediaUrl_PublicPrefix.Length, slashOffset - MediaUrl_PublicPrefix.Length);

                    NameValueCollection queryParams = new UrlBuilder(relativeUrl).GetQueryParameters();

                    urlKind = UrlKind.Public;
                    return new MediaUrlData
                    {
                        MediaId = mediaId,
                        MediaStore = mediaStore,
                        QueryParameters = queryParams
                    };
                }
            }

            return null;
        }

        private static bool TryExtractMediaId(string url, int offset, out Guid mediaId)
        {
            // Extracting a guid, which is kept in plain or compressed as base64 like form
            if((url.Length >= offset + 36 && Guid.TryParse(url.Substring(offset, 36), out mediaId))
                /*|| (url.Length >= offset + 22 && UrlUtils.TryExpandGuid(url.Substring(offset, 22), out mediaId))*/)
            {
                return true;
            }

            mediaId = Guid.Empty;
            return false;
        }

        private static MediaUrlData ParseRenderUrl(string relativeUrl, out UrlKind urlKind)
        {
            try
            {
                var queryParameters = new UrlBuilder(relativeUrl).GetQueryParameters();
                IMediaFile mediaFile = MediaUrlHelper.GetFileFromQueryString(queryParameters);
                Verify.IsNotNull(mediaFile, "failed to get file from a query string");

                urlKind = UrlKind.Renderer;

                queryParameters.Remove("id");
                queryParameters.Remove("i");
                queryParameters.Remove("src");
                queryParameters.Remove("store");

                return new MediaUrlData { MediaId = mediaFile.Id, MediaStore = mediaFile.StoreId, QueryParameters = queryParameters };
            }
            catch(Exception)
            {
                urlKind = UrlKind.Undefined;
                return null;
            }
        }

        private static MediaUrlData ParseInternalUrl(string relativeUrl, string urlPrefix)
        {
            int endBracketOffset = relativeUrl.IndexOf(")", StringComparison.Ordinal);
            if (endBracketOffset < 0) return null;

            string mediaStoreAndId = relativeUrl.Substring(urlPrefix.Length, endBracketOffset - urlPrefix.Length);

            string store = null;
            string mediaIdStr;

            int separatorIndex = mediaStoreAndId.LastIndexOf(":", StringComparison.Ordinal);
            if (separatorIndex > 0)
            {
                store = mediaStoreAndId.Substring(0, separatorIndex);
                mediaIdStr = mediaStoreAndId.Substring(separatorIndex + 1);
            }
            else
            {
                mediaIdStr = mediaStoreAndId;
            }

            Guid mediaId;
            if (!Guid.TryParse(mediaIdStr, out mediaId))
            {
                return null;
            }

            NameValueCollection queryParams = new UrlBuilder(relativeUrl).GetQueryParameters();

            return new MediaUrlData
                       {
                           MediaId = mediaId,
                           MediaStore = store ?? DefaultMediaStore,
                           QueryParameters = queryParams
                       };
        }

        /// <summary>
        /// Builds the URL.
        /// </summary>
        /// <param name="mediaFile">The media file.</param>
        /// <param name="urlKind">Kind of the URL.</param>
        /// <returns></returns>
        public static string BuildUrl(IMediaFile mediaFile, UrlKind urlKind = UrlKind.Public)
        {
            return BuildUrl(new MediaUrlData(mediaFile), urlKind);
        }
        
        /// <summary>
        /// Builds the URL.
        /// </summary>
        /// <param name="mediaUrlData">The media URL data.</param>
        /// <param name="urlKind">Kind of the URL.</param>
        /// <returns></returns>
        public static string BuildUrl(MediaUrlData mediaUrlData, UrlKind urlKind)
        {
            Verify.ArgumentNotNull(mediaUrlData, "mediaUrlData");

            switch (urlKind)
            {
                case UrlKind.Internal:
                    return BuildInternalUrl(mediaUrlData);
                case UrlKind.Renderer:
                    return BuildRendererUrl(mediaUrlData);
                case UrlKind.Public:
                    return BuildPublicUrl(mediaUrlData);
            }

            throw new NotSupportedException("Not supported url kind. urlKind == '{0}'".FormatWith(urlKind));
        }

        private static string BuildInternalUrl(MediaUrlData mediaUrlData)
        {
            string storeId = mediaUrlData.MediaStore == DefaultMediaStore 
                             ? "" 
                             : mediaUrlData.MediaStore + ":";

            var urlBuilder = new UrlBuilder("~/media(" + storeId + mediaUrlData.MediaId + ")");

            if (mediaUrlData.QueryParameters != null)
            {
                urlBuilder.AddQueryParameters(mediaUrlData.QueryParameters);
            }

            return urlBuilder.ToString();
        }

        private static string BuildRendererUrl(MediaUrlData mediaUrlData)
        {
            var queryParams = new NameValueCollection(mediaUrlData.QueryParameters);

            queryParams.Add("id", mediaUrlData.MediaId.ToString());

            if (mediaUrlData.MediaStore != null 
                && mediaUrlData.MediaStore != DefaultMediaStore)
            {
                queryParams.Add("store", mediaUrlData.MediaStore);
            }

            var url = new UrlBuilder(UrlUtils.PublicRootPath + "/Renderers/ShowMedia.ashx");
            url.AddQueryParameters(queryParams);

            return url;
        }

        private static string BuildPublicUrl(MediaUrlData mediaUrlData)
        {
            var queryParams = new NameValueCollection();

            if (mediaUrlData.QueryParameters != null)
            {
                queryParams.Add(mediaUrlData.QueryParameters);
            }

            IMediaFile file = GetFileById(mediaUrlData.MediaStore, mediaUrlData.MediaId);
            if (file == null)
            {
                return null;
            }

            string pathToFile = UrlUtils.Combine(file.FolderPath, file.FileName);

            pathToFile = RemoveForbiddenCharactersAndNormalize(pathToFile);

            // IIS6 doesn't have wildcard mapping by default, so removing image extension if running in "classic" app pool
            if (!HttpRuntime.UsingIntegratedPipeline)
            {
                int dotOffset = pathToFile.IndexOf(".", StringComparison.Ordinal);
                if (dotOffset >= 0)
                {
                    pathToFile = pathToFile.Substring(0, dotOffset);
                }
            }

            string mediaStore = string.Empty;

            if(!mediaUrlData.MediaStore.Equals(DefaultMediaStore, StringComparison.InvariantCultureIgnoreCase))
            {
                mediaStore = mediaUrlData.MediaStore + "/";
            }


            var url = new UrlBuilder(UrlUtils.PublicRootPath + "/media/" + mediaStore + /* UrlUtils.CompressGuid(*/ mediaUrlData.MediaId /*)*/);

            url.PathInfo = file.LastWriteTime != null
                               ? "/" + GetDateTimeHash(file.LastWriteTime.Value.ToUniversalTime()) : string.Empty;

            if (pathToFile.Length > 0)
            {
                url.PathInfo += pathToFile;
            }
            url.AddQueryParameters(queryParams);

            return url.ToString();
        }

        private static string GetDateTimeHash(DateTime dateTime)
        {
            int hash = dateTime.GetHashCode();
            return Convert.ToBase64String(BitConverter.GetBytes(hash)).Substring(0, 6).Replace('+', '-').Replace('/', '_');
        }

        private static string RemoveFilePathIllegalCharacters(string path)
        {
            path = path.Replace("\"", " ").Replace("<", " ").Replace(">", " ").Replace("|", " ");
            for (int i = 0; i < 31; i++)
            {
                path = path.Replace((char) i, ' ');
            }
            return path;
        }

        private static string RemoveForbiddenCharactersAndNormalize(string path)
        {
            // Replacing dots with underscores, so IIS will not intercept requests in some scenarios

            string legalFilePath = RemoveFilePathIllegalCharacters(path);
            string extension = Path.GetExtension(legalFilePath);

            if (!MimeTypeInfo.IsIisServable(extension))
            {
                path = path.Replace(".", "_");
            }

            path = path.Replace("+", " ");

            foreach (var ch in ForbiddenUrlCharacters)
            {
                path = path.Replace(ch, '#');
            }

            path = path.Replace("#", string.Empty);

            // Removing consequtive white spaces
            while(path.Contains("  "))
            {
                path = path.Replace("  ", " ");
            }

            string[] parts = path.Split('/');

            var result = new StringBuilder();
            for(int i=0; i<parts.Length; i++)
            {
                string trimmedPart = parts[i].Trim();
                if(trimmedPart.Length > 0)
                {
                    result.Append("/").Append(trimmedPart);
                }
            }

            return result.ToString();
        }

        private static IMediaFile GetFileById(string storeId, Guid fileId)
        {
            using (new DataScope(DataScopeIdentifier.Public))
            {
                var query = DataFacade.GetData<IMediaFile>();

                if (query.IsEnumerableQuery())
                {
                    return (query as IEnumerable<IMediaFile>)
                        .FirstOrDefault(f => f.Id == fileId && f.StoreId == storeId);
                }

                return query
                    .FirstOrDefault(f => f.StoreId == storeId && f.Id == fileId);
            }
        }
    }
}
