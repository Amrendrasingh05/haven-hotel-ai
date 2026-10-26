
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectorRef,
  Component,
  OnDestroy,
  OnInit
} from '@angular/core';
import { FormsModule } from '@angular/forms';

interface GuestPortal {
  guestName: string;
  reservationId: number;
  roomNumber: string;
  expiresAtUtc: string;
}

interface ServiceRequestResponse {
  id?: number;
  message?: string;
  department?: string;
  status?: string;
}

interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

interface AiChatResponse {
  answer: string;
}

interface SpeechRecognitionResultEvent extends Event {
  readonly results: SpeechRecognitionResultList;
}

interface SpeechRecognitionErrorEvent extends Event {
  readonly error: string;
}

interface SpeechRecognitionLike {
  lang: string;
  continuous: boolean;
  interimResults: boolean;
  onresult: ((event: SpeechRecognitionResultEvent) => void) | null;
  onerror: ((event: SpeechRecognitionErrorEvent) => void) | null;
  onend: (() => void) | null;
  start(): void;
  stop(): void;
  abort(): void;
}

type SpeechRecognitionConstructor = new () => SpeechRecognitionLike;

@Component({
  selector: 'app-guest',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './guest.html',
  styleUrl: './guest.scss'
})
export class Guest implements OnInit, OnDestroy {
  private readonly apiUrl = 'http://localhost:5131/api';

  // Guest login
  reservationId: number | null = null;
  roomNumber = '';
  phone = '';

  loading = false;
  error = '';
  message = '';

  // Authenticated guest session
  token = '';
  guest: GuestPortal | null = null;

  // Manual service request form
  serviceDescription = '';
  serviceCategory = 'General';
  serviceLoading = false;
  serviceError = '';
  serviceMessage = '';

  // AI chat
  chatMessages: ChatMessage[] = [];
  chatInput = '';
  chatLoading = false;
  chatError = '';

  // Ticket confirmation
  showTicketConfirmation = false;
  ticketDescription = '';
  ticketLoading = false;
  ticketError = '';
  ticketMessage = '';

  // Voice assistance
  voiceLanguage: 'en-IN' | 'hi-IN' = 'en-IN';
  isListening = false;
  isSpeaking = false;
  voiceSupported = false;
  voiceMessage = '';
  private recognition: SpeechRecognitionLike | null = null;
  private shouldRestartRecognition = false;

  constructor(
    private readonly http: HttpClient,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.voiceSupported = this.getRecognitionConstructor() !== null;

    const savedToken = sessionStorage.getItem('havenGuestSessionToken');

    if (!savedToken) {
      return;
    }

    this.token = savedToken;

    this.http.get<GuestPortal>(
      `${this.apiUrl}/guest-portal/me`,
      { headers: { 'X-Guest-Session': savedToken } }
    ).subscribe({
      next: guest => {
        this.guest = guest;
        this.cdr.detectChanges();
      },
      error: () => {
        sessionStorage.removeItem('havenGuestSessionToken');
        this.token = '';
        this.guest = null;
        this.cdr.detectChanges();
      }
    });
  }

  ngOnDestroy(): void {
    this.shouldRestartRecognition = false;

    if (this.recognition) {
      this.recognition.onresult = null;
      this.recognition.onerror = null;
      this.recognition.onend = null;

      try {
        this.recognition.abort();
      } catch {
        // Recognition may already have stopped.
      }

      this.recognition = null;
    }

    this.stopSpeaking();
  }

  private getRecognitionConstructor(): SpeechRecognitionConstructor | null {
    const browser = window as unknown as {
      SpeechRecognition?: SpeechRecognitionConstructor;
      webkitSpeechRecognition?: SpeechRecognitionConstructor;
    };

    return browser.SpeechRecognition ??
      browser.webkitSpeechRecognition ??
      null;
  }

  login(): void {
    this.error = '';
    this.message = '';

    const reservationId = Number(this.reservationId);
    const roomNumber = this.roomNumber.trim();
    const phone = this.phone.trim();

    if (
      !Number.isInteger(reservationId) ||
      reservationId <= 0 ||
      !roomNumber ||
      !phone
    ) {
      this.error =
        'Enter your reservation ID, room number and phone number.';
      return;
    }

    this.loading = true;

    this.http.post<{
      token: string;
      expiresAtUtc: string;
      guestName: string;
      reservationId: number;
      roomNumber: string;
    }>(`${this.apiUrl}/guest-sessions/verify`, {
      reservationId,
      roomNumber,
      phone
    }).subscribe({
      next: response => {
        this.token = response.token;

        sessionStorage.setItem(
          'havenGuestSessionToken',
          response.token
        );

        this.guest = {
          guestName: response.guestName,
          reservationId: response.reservationId,
          roomNumber: response.roomNumber,
          expiresAtUtc: response.expiresAtUtc
        };

        this.message = 'Verification successful.';
        this.error = '';
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        this.error = err.error?.message ??
          'Unable to verify your reservation. Please check your details.';

        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  // Voice controls
  toggleListening(): void {
    if (!this.guest || !this.token || this.chatLoading) {
      return;
    }

    if (this.isListening) {
      this.stopListening();
      return;
    }

    this.startListening();
  }

  startListening(): void {
    const Recognition = this.getRecognitionConstructor();

    if (!Recognition) {
      this.voiceSupported = false;
      this.voiceMessage =
        'Speech recognition is not supported by this browser. You can still type your message.';
      this.cdr.detectChanges();
      return;
    }

    if (!this.guest || !this.token) {
      this.voiceMessage = 'Please verify your reservation first.';
      return;
    }

    this.stopSpeaking();

    try {
      this.recognition?.abort();
    } catch {
      // Ignore an already-stopped recognizer.
    }

    this.recognition = new Recognition();
    this.recognition.lang = this.voiceLanguage;
    this.recognition.continuous = false;
    this.recognition.interimResults = false;
    this.shouldRestartRecognition = true;
    this.voiceMessage = 'Listening… Speak now.';
    this.chatError = '';

    this.recognition.onresult = (event: SpeechRecognitionResultEvent) => {
      const result = event.results[event.results.length - 1];
      const transcript = result?.[0]?.transcript?.trim();

      if (transcript) {
        this.chatInput = transcript;
        this.voiceMessage = 'Speech captured. Review it, then send.';
        this.cdr.detectChanges();
      }
    };

    this.recognition.onerror = (event: SpeechRecognitionErrorEvent) => {
      this.isListening = false;

      const messages: Record<string, string> = {
        'not-allowed': 'Microphone permission was denied. Allow microphone access in your browser settings.',
        'service-not-allowed': 'This browser does not allow speech recognition.',
        'no-speech': 'No speech was detected. Please try again.',
        'audio-capture': 'No working microphone was found.',
        network: 'Browser speech recognition reported a network error. Try again or type your message.'
      };

      this.voiceMessage = messages[event.error] ??
        `Speech recognition stopped (${event.error}). You can type instead.`;

      this.cdr.detectChanges();
    };

    this.recognition.onend = () => {
      this.isListening = false;
      this.recognition = null;
      this.cdr.detectChanges();
    };

    try {
      this.recognition.start();
      this.isListening = true;
    } catch {
      this.isListening = false;
      this.voiceMessage = 'Could not start the microphone. Please try again.';
    }

    this.cdr.detectChanges();
  }

  stopListening(): void {
    this.shouldRestartRecognition = false;

    if (this.recognition) {
      try {
        this.recognition.stop();
      } catch {
        // Ignore if recognition already stopped.
      }
    }

    this.isListening = false;
    this.voiceMessage = 'Microphone stopped.';
    this.cdr.detectChanges();
  }

  toggleVoiceLanguage(): void {
    this.voiceLanguage =
      this.voiceLanguage === 'en-IN' ? 'hi-IN' : 'en-IN';

    if (this.isListening) {
      this.stopListening();
    }

    this.voiceMessage =
      this.voiceLanguage === 'hi-IN'
        ? 'Hindi speech recognition selected.'
        : 'English (India) speech recognition selected.';
  }

  speakText(text: string): void {
    if (!('speechSynthesis' in window) || !text.trim()) {
      this.voiceMessage = 'Spoken responses are not supported by this browser.';
      return;
    }

    this.stopSpeaking();

    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = this.voiceLanguage;
    utterance.rate = 1;
    utterance.pitch = 1;

    utterance.onstart = () => {
      this.isSpeaking = true;
      this.cdr.detectChanges();
    };

    utterance.onend = () => {
      this.isSpeaking = false;
      this.cdr.detectChanges();
    };

    utterance.onerror = () => {
      this.isSpeaking = false;
      this.voiceMessage = 'Could not play the spoken response.';
      this.cdr.detectChanges();
    };

    window.speechSynthesis.speak(utterance);
  }

  stopSpeaking(): void {
    if ('speechSynthesis' in window) {
      window.speechSynthesis.cancel();
    }

    this.isSpeaking = false;
  }

  // Send the latest message to the secured local AI endpoint.
  sendChatMessage(): void {
    const content = this.chatInput.trim();

    if (!content || this.chatLoading || !this.guest || !this.token) {
      return;
    }

    if (content.length > 2000) {
      this.chatError = 'Please keep your message under 2,000 characters.';
      return;
    }

    if (this.chatMessages.length >= 20) {
      this.chatError = 'Please start a new conversation.';
      return;
    }

    this.stopListening();
    this.chatError = '';
    this.ticketError = '';
    this.ticketMessage = '';
    this.showTicketConfirmation = false;

    this.chatMessages.push({ role: 'user', content });
    this.chatInput = '';
    this.chatLoading = true;
    this.voiceMessage = '';

    this.http.post<AiChatResponse>(
      `${this.apiUrl}/ai/chat`,
      { message: content },
      { headers: { 'X-Guest-Session': this.token } }
    ).subscribe({
      next: response => {
        const answer = response.answer ??
          'I could not generate a response. Please try again.';

        this.chatMessages.push({
          role: 'assistant',
          content: answer
        });

        this.chatLoading = false;
        this.cdr.detectChanges();

        // Speak the answer only if the guest enabled spoken responses.
        if (this.autoSpeak) {
          this.speakText(answer);
        }
      },
      error: err => {
        this.chatError = err.error?.message ??
          'Unable to reach the AI assistant. Check that the backend and Ollama are running.';

        this.chatLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  autoSpeak = false;

  toggleAutoSpeak(): void {
    this.autoSpeak = !this.autoSpeak;

    if (!this.autoSpeak) {
      this.stopSpeaking();
    }

    this.voiceMessage = this.autoSpeak
      ? 'Spoken AI responses enabled.'
      : 'Spoken AI responses disabled.';
  }

  prepareTicket(): void {
    const userMessages = this.chatMessages
      .filter(m => m.role === 'user')
      .map(m => m.content.trim());

    if (!userMessages.length) {
      this.ticketError = 'Please describe your issue in the chat first.';
      return;
    }

    this.ticketDescription = userMessages.join('\n').slice(0, 500);
    this.ticketError = '';
    this.ticketMessage = '';
    this.showTicketConfirmation = true;
  }

  confirmTicket(): void {
    const description = this.ticketDescription.trim();

    if (!this.guest || !this.token) {
      this.ticketError = 'Please log in again before creating a ticket.';
      return;
    }

    if (!description || description.length > 500) {
      this.ticketError = 'Ticket description must be between 1 and 500 characters.';
      return;
    }

    this.ticketLoading = true;
    this.ticketError = '';
    this.ticketMessage = '';

    this.http.post<ServiceRequestResponse>(
      `${this.apiUrl}/service-requests`,
      { description },
      { headers: { 'X-Guest-Session': this.token } }
    ).subscribe({
      next: response => {
        this.ticketMessage = response.message ??
          `Ticket${response.id ? ` #${response.id}` : ''} created successfully.`;

        this.ticketLoading = false;
        this.showTicketConfirmation = false;
        this.ticketDescription = '';
        this.cdr.detectChanges();
      },
      error: err => {
        this.ticketError = err.error?.message ??
          'Unable to create the ticket. Please try again.';

        this.ticketLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  cancelTicketConfirmation(): void {
    this.showTicketConfirmation = false;
    this.ticketError = '';
  }

  startNewChat(): void {
    if (this.chatLoading || this.ticketLoading) {
      return;
    }

    this.stopSpeaking();
    this.stopListening();
    this.chatMessages = [];
    this.chatInput = '';
    this.chatError = '';
    this.ticketError = '';
    this.ticketMessage = '';
    this.ticketDescription = '';
    this.showTicketConfirmation = false;
  }

  submitServiceRequest(): void {
    this.serviceError = '';
    this.serviceMessage = '';

    const description = this.serviceDescription.trim();

    if (!this.guest || !this.token) {
      this.serviceError = 'Please log in again to submit a request.';
      return;
    }

    if (!description || description.length > 500) {
      this.serviceError =
        'Enter a description between 1 and 500 characters.';
      return;
    }

    this.serviceLoading = true;

    this.http.post<ServiceRequestResponse>(
      `${this.apiUrl}/service-requests`,
      { description },
      { headers: { 'X-Guest-Session': this.token } }
    ).subscribe({
      next: response => {
        this.serviceMessage = response.message ??
          `Request submitted successfully${response.id ? ` (#${response.id})` : ''}.`;

        this.serviceDescription = '';
        this.serviceError = '';
        this.serviceLoading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        this.serviceError = err.error?.message ??
          'Unable to submit your request. Please try again.';

        this.serviceLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  logout(): void {
    this.stopListening();
    this.stopSpeaking();

    const token = this.token;

    if (!token) {
      this.clearSession();
      return;
    }

    this.http.post(
      `${this.apiUrl}/guest-sessions/logout`,
      {},
      { headers: { 'X-Guest-Session': token } }
    ).subscribe({
      next: () => {
        this.clearSession();
        this.cdr.detectChanges();
      },
      error: () => {
        this.clearSession();
        this.cdr.detectChanges();
      }
    });
  }

  private clearSession(): void {
    sessionStorage.removeItem('havenGuestSessionToken');

    this.token = '';
    this.guest = null;

    this.reservationId = null;
    this.roomNumber = '';
    this.phone = '';

    this.message = '';
    this.error = '';

    this.serviceDescription = '';
    this.serviceCategory = 'General';
    this.serviceError = '';
    this.serviceMessage = '';
    this.serviceLoading = false;

    this.chatMessages = [];
    this.chatInput = '';
    this.chatLoading = false;
    this.chatError = '';

    this.showTicketConfirmation = false;
    this.ticketDescription = '';
    this.ticketLoading = false;
    this.ticketError = '';
    this.ticketMessage = '';

    this.autoSpeak = false;
    this.voiceMessage = '';
    this.isListening = false;
    this.isSpeaking = false;
  }
}
