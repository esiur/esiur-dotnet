using Esiur.Data;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Esiur.Net.Packets.WebSocket;

public class WebsocketPacket : Packet
{
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

    public enum WSOpcode : byte
    {
        ContinuationFrame = 0x0,
        TextFrame = 0x1,
        BinaryFrame = 0x2,
        ConnectionClose = 0x8,
        Ping = 0x9,
        Pong = 0xA,
    }

    public const ulong DefaultMaximumPayloadLength = 8 * 1024 * 1024;

    public bool FIN;
    public bool RSV1;
    public bool RSV2;
    public bool RSV3;
    public WSOpcode Opcode;
    public bool Mask;
    public long PayloadLength;
    public byte[] MaskKey;
    public byte[] Message;

    /// <summary>
    /// Maximum accepted or composed payload. Set to zero to disable the limit.
    /// </summary>
    public ulong MaximumPayloadLength { get; set; } = DefaultMaximumPayloadLength;

    /// <summary>
    /// Expected mask bit for an incoming frame. Servers set this to <c>true</c>,
    /// clients set it to <c>false</c>, and standalone packet parsing can leave it
    /// <c>null</c> to accept either direction.
    /// </summary>
    public bool? ExpectedMask { get; set; }

    public override string ToString()
        => $"WebsocketPacket\n\tFIN: {FIN}\n\tOpcode: {Opcode}\n\tPayload: {PayloadLength}" +
           $"\n\tMaskKey: {MaskKey}\n\tMessage: {(Message == null ? "NULL" : Message.Length.ToString())}";

    public override bool Compose()
    {
        var message = Message ?? Array.Empty<byte>();
        ValidateFrame(Opcode, FIN, (ulong)message.LongLength);
        ValidateApplicationPayload(Opcode, FIN, message);
        EnsureWithinLimit((ulong)message.LongLength);

        var extendedLengthSize = message.Length <= 125
            ? 0
            : message.Length <= ushort.MaxValue ? 2 : 8;
        var headerLength = 2 + extendedLengthSize + (Mask ? 4 : 0);
        Data = new byte[checked(headerLength + message.Length)];

        var offset = 0;
        Data[offset++] = (byte)((FIN ? 0x80 : 0) |
                                (RSV1 ? 0x40 : 0) |
                                (RSV2 ? 0x20 : 0) |
                                (RSV3 ? 0x10 : 0) |
                                (byte)Opcode);

        if (extendedLengthSize == 0)
        {
            Data[offset++] = (byte)((Mask ? 0x80 : 0) | message.Length);
        }
        else if (extendedLengthSize == 2)
        {
            Data[offset++] = (byte)((Mask ? 0x80 : 0) | 126);
            Data[offset++] = (byte)(message.Length >> 8);
            Data[offset++] = (byte)message.Length;
        }
        else
        {
            Data[offset++] = (byte)((Mask ? 0x80 : 0) | 127);
            var length = (ulong)message.LongLength;
            for (var shift = 56; shift >= 0; shift -= 8)
                Data[offset++] = (byte)(length >> shift);
        }

        if (Mask)
        {
            if (MaskKey == null || MaskKey.Length != 4)
            {
                MaskKey = new byte[4];
                using (var random = RandomNumberGenerator.Create())
                    random.GetBytes(MaskKey);
            }

            Buffer.BlockCopy(MaskKey, 0, Data, offset, MaskKey.Length);
            offset += MaskKey.Length;

            for (var i = 0; i < message.Length; i++)
                Data[offset + i] = (byte)(message[i] ^ MaskKey[i & 3]);
        }
        else if (message.Length > 0)
        {
            Buffer.BlockCopy(message, 0, Data, offset, message.Length);
        }

        PayloadLength = message.LongLength;
        return true;
    }

    public override long Parse(byte[] data, uint offset, uint ends)
    {
        ValidateBounds(data, offset, ends);
        var originalOffset = offset;

        if (TryGetMissingBytes(offset, ends, 2, out var incomplete))
            return incomplete;

        var first = data[offset++];
        var second = data[offset++];

        FIN = (first & 0x80) != 0;
        RSV1 = (first & 0x40) != 0;
        RSV2 = (first & 0x20) != 0;
        RSV3 = (first & 0x10) != 0;
        Opcode = (WSOpcode)(first & 0x0F);
        Mask = (second & 0x80) != 0;

        if (ExpectedMask.HasValue && Mask != ExpectedMask.Value)
            throw new InvalidDataException(ExpectedMask.Value
                ? "WebSocket clients must mask every frame sent to a server."
                : "WebSocket servers must not mask frames sent to a client.");

        if (RSV1 || RSV2 || RSV3)
            throw new InvalidDataException("WebSocket extensions are not enabled for this connection.");

        ulong payloadLength = (byte)(second & 0x7F);
        if (payloadLength == 126)
        {
            if (TryGetMissingBytes(offset, ends, 2, out incomplete))
                return incomplete;

            payloadLength = (uint)(data[offset] << 8 | data[offset + 1]);
            offset += 2;

            if (payloadLength < 126)
                throw new InvalidDataException("WebSocket payload length is not minimally encoded.");
        }
        else if (payloadLength == 127)
        {
            if (TryGetMissingBytes(offset, ends, 8, out incomplete))
                return incomplete;
            if ((data[offset] & 0x80) != 0)
                throw new InvalidDataException("WebSocket payload length exceeds the protocol limit.");

            payloadLength = 0;
            for (var i = 0; i < 8; i++)
                payloadLength = payloadLength << 8 | data[offset++];

            if (payloadLength <= ushort.MaxValue)
                throw new InvalidDataException("WebSocket payload length is not minimally encoded.");
        }

        ValidateFrame(Opcode, FIN, payloadLength);
        EnsureWithinLimit(payloadLength);
        if (payloadLength > int.MaxValue)
            throw new ParserLimitException("WebSocket payload cannot fit in a managed byte array.");

        if (Mask)
        {
            if (TryGetMissingBytes(offset, ends, 4, out incomplete))
                return incomplete;

            MaskKey = new byte[4];
            Buffer.BlockCopy(data, (int)offset, MaskKey, 0, MaskKey.Length);
            offset += 4;
        }
        else
        {
            MaskKey = null;
        }

        var availablePayload = ends - offset;
        if ((ulong)availablePayload < payloadLength)
            return -(long)(payloadLength - availablePayload);

        Message = new byte[(int)payloadLength];
        if (Mask)
        {
            for (var i = 0; i < Message.Length; i++)
                Message[i] = (byte)(data[offset + i] ^ MaskKey[i & 3]);
        }
        else if (Message.Length > 0)
        {
            Buffer.BlockCopy(data, (int)offset, Message, 0, Message.Length);
        }

        offset += (uint)payloadLength;
        PayloadLength = (long)payloadLength;
        ValidateApplicationPayload(Opcode, FIN, Message);
        return offset - originalOffset;
    }

    private void EnsureWithinLimit(ulong payloadLength)
    {
        if (MaximumPayloadLength > 0 && payloadLength > MaximumPayloadLength)
            throw new ParserLimitException(
                $"WebSocket payload of {payloadLength} bytes exceeds the {MaximumPayloadLength}-byte limit.");
    }

    private static void ValidateFrame(WSOpcode opcode, bool final, ulong payloadLength)
    {
        var isControl = opcode == WSOpcode.ConnectionClose ||
                        opcode == WSOpcode.Ping ||
                        opcode == WSOpcode.Pong;
        var isData = opcode == WSOpcode.ContinuationFrame ||
                     opcode == WSOpcode.TextFrame ||
                     opcode == WSOpcode.BinaryFrame;

        if (!isControl && !isData)
            throw new InvalidDataException($"Unsupported WebSocket opcode: 0x{(byte)opcode:X}.");
        if (isControl && (!final || payloadLength > 125))
            throw new InvalidDataException("WebSocket control frames must be final and at most 125 bytes.");
        if (opcode == WSOpcode.ConnectionClose && payloadLength == 1)
            throw new InvalidDataException("A WebSocket close frame cannot contain a one-byte payload.");
    }

    private static void ValidateApplicationPayload(WSOpcode opcode, bool final, byte[] payload)
    {
        if (opcode == WSOpcode.TextFrame && final)
            ValidateTextPayload(payload);
        else if (opcode == WSOpcode.ConnectionClose)
            ValidateClosePayload(payload);
    }

    internal static void ValidateTextPayload(byte[] payload)
        => ValidateTextPayload(payload ?? Array.Empty<byte>(), 0, payload?.Length ?? 0);

    private static void ValidateTextPayload(byte[] payload, int offset, int count)
    {
        try
        {
            _ = StrictUtf8.GetCharCount(payload, offset, count);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("WebSocket text payload is not valid UTF-8.", exception);
        }
    }

    private static void ValidateClosePayload(byte[] payload)
    {
        if (payload == null || payload.Length < 2)
            return;

        var statusCode = payload[0] << 8 | payload[1];
        var isDefinedProtocolCode = statusCode >= 1000 && statusCode <= 1014
            && statusCode != 1004
            && statusCode != 1005
            && statusCode != 1006;
        var isApplicationCode = statusCode >= 3000 && statusCode <= 4999;

        if (!isDefinedProtocolCode && !isApplicationCode)
            throw new InvalidDataException($"Invalid WebSocket close status code: {statusCode}.");

        if (payload.Length > 2)
            ValidateTextPayload(payload, 2, payload.Length - 2);
    }
}
