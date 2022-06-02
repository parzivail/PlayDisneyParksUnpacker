namespace PlayDisneyParksUnpacker;

public class UnpackException : Exception
{
	public ErrorCode Code { get; }

	public UnpackException(ErrorCode code, string message) : base(message)
	{
		Code = code;
	}

	public UnpackException(ErrorCode code, Exception cause) : base(cause.Message, cause)
	{
		Code = code;
	}
}