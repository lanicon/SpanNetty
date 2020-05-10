﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Common.Utilities;

    public static class HttpUtil
    {
        const int IndexNotFound = -1;
        const uint NIndexNotFound = unchecked((uint)IndexNotFound);

        static readonly AsciiString CharsetEquals = new AsciiString(HttpHeaderValues.Charset + "=");
        static readonly AsciiString Semicolon = AsciiString.Cached(";");

        ///// <summary>
        ///// Determine if a uri is in origin-form according to
        ///// <a href="https://tools.ietf.org/html/rfc7230#section-5.3">rfc7230, 5.3</a>.
        ///// </summary>
        ///// <param name="uri"></param>
        ///// <returns></returns>
        //public static bool IsOriginForm(Uri uri)
        //{
        //    return uri.Scheme == null && /*uri.getSchemeSpecificPart() == null &&*/ uri.PathAndQuery == null &&
        //           uri.Host == null && uri.Authority == null;
        //}

        ///// <summary>
        ///// Determine if a uri is in asterisk-form according to
        ///// <a href="https://tools.ietf.org/html/rfc7230#section-5.3">rfc7230, 5.3</a>.
        ///// </summary>
        ///// <param name="uri"></param>
        ///// <returns></returns>
        //public static bool IsAsteriskForm(Uri uri)
        //{
        //    return string.Equals("*", uri.AbsolutePath, StringComparison.Ordinal) &&
        //           uri.Scheme == null && /*uri.getSchemeSpecificPart() == null &&*/ uri.Host == null && uri.PathAndQuery == null &&
        //           uri.Port == 0 && uri.Authority == null && uri.Query == null &&
        //           uri.Fragment == null;
        //}

        public static bool IsKeepAlive(IHttpMessage message)
        {
            if (message.Headers.TryGet(HttpHeaderNames.Connection, out ICharSequence connection)
                && HttpHeaderValues.Close.ContentEqualsIgnoreCase(connection))
            {
                return false;
            }

            if (message.ProtocolVersion.IsKeepAliveDefault)
            {
                return !HttpHeaderValues.Close.ContentEqualsIgnoreCase(connection);
            }
            else
            {
                return HttpHeaderValues.KeepAlive.ContentEqualsIgnoreCase(connection);
            }
        }

        public static void SetKeepAlive(IHttpMessage message, bool keepAlive) => SetKeepAlive(message.Headers, message.ProtocolVersion, keepAlive);

        public static void SetKeepAlive(HttpHeaders headers, HttpVersion httpVersion, bool keepAlive)
        {
            if (httpVersion.IsKeepAliveDefault)
            {
                if (keepAlive)
                {
                    headers.Remove(HttpHeaderNames.Connection);
                }
                else
                {
                    headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.Close);
                }
            }
            else
            {
                if (keepAlive)
                {
                    headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.KeepAlive);
                }
                else
                {
                    headers.Remove(HttpHeaderNames.Connection);
                }
            }
        }

        public static long GetContentLength(IHttpMessage message)
        {
            if (message.Headers.TryGet(HttpHeaderNames.ContentLength, out ICharSequence value))
            {
                return CharUtil.ParseLong(value);
            }

            // We know the content length if it's a Web Socket message even if
            // Content-Length header is missing.
            long webSocketContentLength = GetWebSocketContentLength(message);
            if (webSocketContentLength >= 0)
            {
                return webSocketContentLength;
            }

            // Otherwise we don't.
            return ThrowHelper.ThrowFormatException_HeaderNotFound();
        }

        public static long GetContentLength(IHttpMessage message, long defaultValue)
        {
            if (message.Headers.TryGet(HttpHeaderNames.ContentLength, out ICharSequence value))
            {
                return CharUtil.ParseLong(value);
            }

            // We know the content length if it's a Web Socket message even if
            // Content-Length header is missing.
            long webSocketContentLength = GetWebSocketContentLength(message);
            if (webSocketContentLength >= 0)
            {
                return webSocketContentLength;
            }

            // Otherwise we don't.
            return defaultValue;
        }

        public static int GetContentLength(IHttpMessage message, int defaultValue) =>
            (int)Math.Min(int.MaxValue, GetContentLength(message, (long)defaultValue));

        static int GetWebSocketContentLength(IHttpMessage message)
        {
            // WebSocket messages have constant content-lengths.
            HttpHeaders h = message.Headers;
            switch (message)
            {
                case IHttpRequest req:
                    if (HttpMethod.Get.Equals(req.Method)
                        && h.Contains(HttpHeaderNames.SecWebsocketKey1)
                        && h.Contains(HttpHeaderNames.SecWebsocketKey2))
                    {
                        return 8;
                    }
                    break;

                case IHttpResponse res:
                    if (res.Status.Code == StatusCodes.Status101SwitchingProtocols
                        && h.Contains(HttpHeaderNames.SecWebsocketOrigin)
                        && h.Contains(HttpHeaderNames.SecWebsocketLocation))
                    {
                        return 16;
                    }
                    break;
            }

            // Not a web socket message
            return -1;
        }

        public static void SetContentLength(IHttpMessage message, long length) => message.Headers.Set(HttpHeaderNames.ContentLength, length);

        public static bool IsContentLengthSet(IHttpMessage message) => message.Headers.Contains(HttpHeaderNames.ContentLength);

        public static bool Is100ContinueExpected(IHttpMessage message)
        {
            if (!IsExpectHeaderValid(message))
            {
                return false;
            }

            ICharSequence expectValue = message.Headers.Get(HttpHeaderNames.Expect, null);
            // unquoted tokens in the expect header are case-insensitive, thus 100-continue is case insensitive
            return HttpHeaderValues.Continue.ContentEqualsIgnoreCase(expectValue);
        }

        internal static bool IsUnsupportedExpectation(IHttpMessage message)
        {
            if (!IsExpectHeaderValid(message))
            {
                return false;
            }

            return message.Headers.TryGet(HttpHeaderNames.Expect, out ICharSequence expectValue)
                && !HttpHeaderValues.Continue.ContentEqualsIgnoreCase(expectValue);
        }

        // Expect: 100-continue is for requests only and it works only on HTTP/1.1 or later. Note further that RFC 7231
        // section 5.1.1 says "A server that receives a 100-continue expectation in an HTTP/1.0 request MUST ignore
        // that expectation."
        static bool IsExpectHeaderValid(IHttpMessage message) => message is IHttpRequest
            && message.ProtocolVersion.CompareTo(HttpVersion.Http11) >= 0;

        public static void Set100ContinueExpected(IHttpMessage message, bool expected)
        {
            if (expected)
            {
                message.Headers.Set(HttpHeaderNames.Expect, HttpHeaderValues.Continue);
            }
            else
            {
                message.Headers.Remove(HttpHeaderNames.Expect);
            }
        }

        public static bool IsTransferEncodingChunked(IHttpMessage message) => message.Headers.Contains(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked, true);

        public static void SetTransferEncodingChunked(IHttpMessage m, bool chunked)
        {
            if (chunked)
            {
                m.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
                m.Headers.Remove(HttpHeaderNames.ContentLength);
            }
            else
            {
                IList<ICharSequence> encodings = m.Headers.GetAll(HttpHeaderNames.TransferEncoding);
                if (0u >= (uint)encodings.Count)
                {
                    return;
                }
                var values = new List<ICharSequence>(encodings);
                foreach (ICharSequence value in encodings)
                {
                    if (HttpHeaderValues.Chunked.ContentEqualsIgnoreCase(value))
                    {
                        values.Remove(value);
                    }
                }
                if (0u >= (uint)values.Count)
                {
                    m.Headers.Remove(HttpHeaderNames.TransferEncoding);
                }
                else
                {
                    m.Headers.Set(HttpHeaderNames.TransferEncoding, values);
                }
            }
        }

        public static Encoding GetCharset(IHttpMessage message) => GetCharset(message, Encoding.UTF8);

        public static Encoding GetCharset(ICharSequence contentTypeValue) => contentTypeValue is object ? GetCharset(contentTypeValue, Encoding.UTF8) : Encoding.UTF8;

        public static Encoding GetCharset(IHttpMessage message, Encoding defaultCharset)
        {
            return message.Headers.TryGet(HttpHeaderNames.ContentType, out ICharSequence contentTypeValue)
                ? GetCharset(contentTypeValue, defaultCharset)
                : defaultCharset;
        }

        public static Encoding GetCharset(ICharSequence contentTypeValue, Encoding defaultCharset)
        {
            if (contentTypeValue is object)
            {
                ICharSequence charsetCharSequence = GetCharsetAsSequence(contentTypeValue);
                if (charsetCharSequence is object)
                {
                    try
                    {
                        return Encoding.GetEncoding(charsetCharSequence.ToString());
                    }
                    catch (ArgumentException)
                    {
                        return defaultCharset;
                    }
                }
                else
                {
                    return defaultCharset;
                }
            }
            else
            {
                return defaultCharset;
            }
        }

        public static ICharSequence GetCharsetAsSequence(IHttpMessage message)
            => message.Headers.TryGet(HttpHeaderNames.ContentType, out ICharSequence contentTypeValue) ? GetCharsetAsSequence(contentTypeValue) : null;

        public static ICharSequence GetCharsetAsSequence(ICharSequence contentTypeValue)
        {
            if (contentTypeValue == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.contentTypeValue);
            }
            int indexOfCharset = AsciiString.IndexOfIgnoreCaseAscii(contentTypeValue, CharsetEquals, 0);
            if ((uint)indexOfCharset >= NIndexNotFound) { return null; }
            int indexOfEncoding = indexOfCharset + CharsetEquals.Count;
            if (indexOfEncoding < contentTypeValue.Count)
            {
                var charsetCandidate = contentTypeValue.SubSequence(indexOfEncoding, contentTypeValue.Count);
                int indexOfSemicolon = AsciiString.IndexOfIgnoreCaseAscii(charsetCandidate, Semicolon, 0);
                if ((uint)indexOfSemicolon >= NIndexNotFound)
                {
                    return charsetCandidate;
                }

                return charsetCandidate.SubSequence(0, indexOfSemicolon);
            }
            return null;
        }

        public static ICharSequence GetMimeType(IHttpMessage message) =>
            message.Headers.TryGet(HttpHeaderNames.ContentType, out ICharSequence contentTypeValue) ? GetMimeType(contentTypeValue) : null;

        public static ICharSequence GetMimeType(ICharSequence contentTypeValue)
        {
            if (contentTypeValue == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.contentTypeValue);
            }
            int indexOfSemicolon = AsciiString.IndexOfIgnoreCaseAscii(contentTypeValue, Semicolon, 0);
            if ((uint)indexOfSemicolon < NIndexNotFound)
            {
                return contentTypeValue.SubSequence(0, indexOfSemicolon);
            }

            return contentTypeValue.Count > 0 ? contentTypeValue : null;
        }
    }
}
