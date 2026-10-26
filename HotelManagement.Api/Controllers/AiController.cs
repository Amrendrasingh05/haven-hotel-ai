
using System.Net.Http.Json;
using System.Text.Json;
using HotelManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Api.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GuestSessionService _guestSessionService;

    public AiController(
        IHttpClientFactory httpClientFactory,
        GuestSessionService guestSessionService)
    {
        _httpClientFactory = httpClientFactory;
        _guestSessionService = guestSessionService;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat(
        [FromBody] AiChatRequest request,
        CancellationToken cancellationToken)
    {
        // Only a verified guest may use the guest AI assistant.
        var token = Request.Headers["X-Guest-Session"].FirstOrDefault();

        var session = await _guestSessionService.ValidateAsync(
            token,
            cancellationToken);

        if (session is null)
        {
            return Unauthorized(new
            {
                message = "Please log in to use the hotel assistant."
            });
        }

        var message = request.Message?.Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(new
            {
                message = "Please enter a message."
            });
        }

        if (message.Length > 2000)
        {
            return BadRequest(new
            {
                message = "Message must be 2000 characters or fewer."
            });
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Ollama");

            var payload = new
            {
                model = "gemma4:e4b",
                stream = false,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content =
                            $"""
                            You are Haven Hotel's friendly guest assistant.
                            The verified guest is staying in room
                            {session.Reservation.Room.RoomNumber}.

                            Respond politely and concisely. Understand English
                            and Hindi; reply in the language the guest uses.

                            Help with hotel questions and service problems.
                            Ask for clarification when needed.

                            Never claim that a service ticket, booking,
                            cancellation, or other action has been completed.
                            You cannot create tickets or perform hotel actions.
                            A guest must review and explicitly confirm a
                            service request in the hotel portal.
                            Do not reveal another guest's private information.
                            """
                    },
                    new
                    {
                        role = "user",
                        content = message
                    }
                }
            };

            using var response = await client.PostAsJsonAsync(
                "api/chat",
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(
                    StatusCodes.Status502BadGateway,
                    new
                    {
                        message = "The local AI service returned an error."
                    });
            }

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty(
                    "message", out var messageElement) ||
                !messageElement.TryGetProperty(
                    "content", out var contentElement))
            {
                return StatusCode(
                    StatusCodes.Status502BadGateway,
                    new
                    {
                        message = "The local AI returned an unexpected response."
                    });
            }

            var answer = contentElement.GetString();

            return Ok(new
            {
                answer = string.IsNullOrWhiteSpace(answer)
                    ? "I'm sorry, I couldn't generate a response. Please try again."
                    : answer
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message = "Ollama is unavailable. Please ensure it is running."
                });
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(
                StatusCodes.Status504GatewayTimeout,
                new
                {
                    message = "The local AI response timed out."
                });
        }
        catch (JsonException)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    message = "The local AI returned an invalid response."
                });
        }
    }
}

public class AiChatRequest
{
    public string Message { get; set; } = string.Empty;
}
