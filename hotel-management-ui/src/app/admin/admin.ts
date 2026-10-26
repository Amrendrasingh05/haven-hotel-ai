
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import {
  ChangeDetectorRef,
  Component,
  OnInit
} from '@angular/core';
import { FormsModule } from '@angular/forms';

interface Room {
  id: number;
  roomNumber: string;
  roomType: string;
  pricePerNight: number;
  capacity: number;
  status: string;
}

interface Reservation {
  id: number;
  guestName: string;
  guestPhone?: string;
  roomId: number;
  roomNumber: string;
  checkInDate: string;
  checkOutDate: string;
  guestCount: number;
  status: string;
  totalPrice: number;
}

interface CreateReservationRequest {
  guestName: string;
  guestPhone: string;
  roomId: number;
  checkInDate: string;
  checkOutDate: string;
  guestCount: number;
}

interface Guest {
  id: number;
  fullName: string;
  phone: string;
  email: string | null;
  createdAtUtc: string;
  reservationCount: number;
}

interface GuestReservation {
  id: number;
  roomId: number;
  roomNumber: string;
  checkInDate: string;
  checkOutDate: string;
  guestCount: number;
  status: string;
  totalPrice: number;
  createdAtUtc: string;
}

interface GuestDetails {
  id: number;
  fullName: string;
  phone: string;
  email: string | null;
  createdAtUtc: string;
  reservations: GuestReservation[];
}

interface ServiceRequest {
  id: number;
  guestId: number | null;
  reservationId: number | null;
  guestName: string;
  roomNumber: string;
  description: string;
  category: string;
  department: string;
  priority: string;
  status: string;
  createdAtUtc: string;
  resolvedAtUtc: string | null;
}

interface AiChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin.html',
  styleUrl: './admin.scss'
})
export class Admin implements OnInit {
  private readonly apiUrl = 'http://localhost:5131/api';

  // ---------------- ADMIN AUTHENTICATION ----------------

  private getAdminHeaders(): { 'X-Admin-Session': string } | null {
    const token = sessionStorage.getItem('havenAdminSessionToken');

    if (!token) {
      this.errorMessage = 'Please sign in as an administrator.';
      void this.router.navigate(['/admin-login']);
      return null;
    }

    return { 'X-Admin-Session': token };
  }

  private handleAdminAuthError(error: any): void {
    if (error?.status === 401) {
      sessionStorage.removeItem('havenAdminSessionToken');
      sessionStorage.removeItem('havenAdminUsername');
      void this.router.navigate(['/admin-login']);
    }
  }

  // ---------------- ROOMS AND RESERVATIONS ----------------

  rooms: Room[] = [];
  reservations: Reservation[] = [];

  availableRooms: Room[] = [];
  checkingAvailability = false;
  availabilityMessage = '';

  loading = false;
  saving = false;

  errorMessage = '';
  successMessage = '';

  reservation: CreateReservationRequest = {
    guestName: '',
    guestPhone: '',
    roomId: 0,
    checkInDate: '',
    checkOutDate: '',
    guestCount: 1
  };

  // ---------------- GUEST PORTAL LOGIN ----------------

  guestLoginReservationId: number | null = null;
  guestLoginRoomNumber = '';
  guestLoginPhone = '';

  guestLoginLoading = false;
  guestLoginError = '';
  guestLoginMessage = '';

  guestSessionToken = '';
  guestPortal: {
    guestName: string;
    reservationId: number;
    roomNumber: string;
    expiresAtUtc: string;
  } | null = null;

  // ---------------- GUEST MANAGEMENT ----------------

  guests: Guest[] = [];
  reservationGuests: Guest[] = [];
  selectedReservationGuestId: number | null = null;
  guestSearch = '';

  guestName = '';
  guestPhone = '';
  guestEmail = '';

  guestSaving = false;
  guestLoading = false;
  guestMessage = '';
  guestError = '';

  selectedGuest: GuestDetails | null = null;
  guestDetailsLoading = false;

  // ---------------- SERVICE REQUESTS ----------------

  serviceRequests: ServiceRequest[] = [];

  serviceRequestDepartment = '';
  serviceRequestStatus = '';

  serviceRequestLoading = false;

  // ---------------- AI CHAT ----------------

  aiMessages: AiChatMessage[] = [
    {
      role: 'assistant',
      content: 'Welcome to Haven Hotel! How may I assist you today?'
    }
  ];

  aiUserMessage = '';
  aiLoading = false;
  aiError = '';

  constructor(
    private http: HttpClient,
    private cdr: ChangeDetectorRef,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.reservation.checkInDate = this.today;
    this.reservation.checkOutDate = this.addDays(this.today, 1);

    this.loadRooms();
    this.loadReservations();
    this.checkRoomAvailability();
    this.loadGuests();
    this.loadReservationGuests();
    this.loadServiceRequests();
  }

  // ---------------- DATE HELPERS ----------------

  get today(): string {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');

    return `${year}-${month}-${day}`;
  }

  private addDays(date: string, days: number): string {
    const result = new Date(`${date}T00:00:00`);
    result.setDate(result.getDate() + days);

    const year = result.getFullYear();
    const month = String(result.getMonth() + 1).padStart(2, '0');
    const day = String(result.getDate()).padStart(2, '0');

    return `${year}-${month}-${day}`;
  }

  // ---------------- ROOMS ----------------

  loadRooms(): void {
    this.http.get<Room[]>(`${this.apiUrl}/rooms`).subscribe({
      next: (rooms) => {
        this.rooms = rooms;
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to load rooms:', error);
        this.errorMessage =
          'Could not load rooms. Check that the backend is running.';
        this.cdr.detectChanges();
      }
    });
  }

  // ---------------- RESERVATIONS ----------------

  loadReservations(): void {
    const headers = this.getAdminHeaders();
    if (!headers) return;

    this.loading = true;

    this.http.get<Reservation[]>(
      `${this.apiUrl}/reservations`,
      { headers }
    ).subscribe({
      next: (reservations) => {
        this.reservations = reservations;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to load reservations:', error);
        this.loading = false;
        this.errorMessage = 'Could not load reservations.';
        this.handleAdminAuthError(error);
        this.cdr.detectChanges();
      }
    });
  }

  checkRoomAvailability(): void {
    const { checkInDate, checkOutDate } = this.reservation;

    this.availableRooms = [];
    this.availabilityMessage = '';

    if (!checkInDate || !checkOutDate) {
      this.checkingAvailability = false;
      return;
    }

    if (checkInDate < this.today) {
      this.availabilityMessage = 'Check-in cannot be in the past.';
      this.checkingAvailability = false;
      return;
    }

    if (checkOutDate <= checkInDate) {
      this.availabilityMessage = 'Check-out must be after check-in.';
      this.checkingAvailability = false;
      return;
    }

    this.checkingAvailability = true;

    this.http.get<Room[]>(
      `${this.apiUrl}/reservations/availability`,
      {
        params: {
          checkInDate,
          checkOutDate
        }
      }
    ).subscribe({
      next: (rooms) => {
        this.availableRooms = rooms;
        this.checkingAvailability = false;

        const selectedRoomStillAvailable = rooms.some(
          room => room.id === Number(this.reservation.roomId)
        );

        if (!selectedRoomStillAvailable) {
          this.reservation.roomId = rooms[0]?.id ?? 0;
        }

        this.availabilityMessage = rooms.length > 0
          ? `${rooms.length} room(s) available for these dates.`
          : 'No rooms are available for these dates.';

        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Availability check failed:', error);
        this.availableRooms = [];
        this.checkingAvailability = false;
        this.availabilityMessage =
          'Could not check availability. Please try again.';
        this.cdr.detectChanges();
      }
    });
  }

  logoutAdmin(): void {
    const token = sessionStorage.getItem('havenAdminSessionToken');

    const clearSession = (): void => {
      sessionStorage.removeItem('havenAdminSessionToken');
      sessionStorage.removeItem('havenAdminUsername');
      void this.router.navigate(['/admin-login']);
    };

    if (!token) {
      clearSession();
      return;
    }

    this.http.post(
      `${this.apiUrl}/admin-auth/logout`,
      {},
      { headers: { 'X-Admin-Session': token } }
    ).subscribe({
      next: () => clearSession(),
      error: () => clearSession()
    });
  }

  get selectedRoom(): Room | undefined {
    return this.availableRooms.find(
      room => room.id === Number(this.reservation.roomId)
    );
  }

  get totalNights(): number {
    const { checkInDate, checkOutDate } = this.reservation;

    if (!checkInDate || !checkOutDate) return 0;

    const checkIn = new Date(`${checkInDate}T00:00:00`);
    const checkOut = new Date(`${checkOutDate}T00:00:00`);
    const difference = checkOut.getTime() - checkIn.getTime();

    return Math.max(
      0,
      Math.round(difference / (1000 * 60 * 60 * 24))
    );
  }

  get estimatedTotal(): number {
    return (this.selectedRoom?.pricePerNight ?? 0) * this.totalNights;
  }

  get availableRoomCount(): number {
    return this.availableRooms.length;
  }

  get confirmedReservationCount(): number {
    return this.reservations.filter(
      reservation => reservation.status === 'Confirmed'
    ).length;
  }

  createReservation(): void {
    this.errorMessage = '';
    this.successMessage = '';

    const guestName = this.reservation.guestName.trim();
    const guestPhone = this.reservation.guestPhone.trim();
    const roomId = Number(this.reservation.roomId);
    const guestCount = Number(this.reservation.guestCount);

    if (
      !guestName ||
      !guestPhone ||
      !roomId ||
      !this.reservation.checkInDate ||
      !this.reservation.checkOutDate ||
      !Number.isInteger(guestCount) ||
      guestCount < 1
    ) {
      this.errorMessage = 'Please complete all fields with valid details.';
      return;
    }

    if (this.reservation.checkInDate < this.today) {
      this.errorMessage = 'Check-in cannot be in the past.';
      return;
    }

    if (this.totalNights < 1) {
      this.errorMessage = 'Check-out must be after check-in.';
      return;
    }

    const room = this.selectedRoom;

    if (!room) {
      this.errorMessage =
        'Please select a room available for these dates.';
      return;
    }

    if (guestCount > room.capacity) {
      this.errorMessage =
        `This room accommodates a maximum of ${room.capacity} guests.`;
      return;
    }

    const headers = this.getAdminHeaders();
    if (!headers) return;

    const payload: CreateReservationRequest = {
      guestName,
      guestPhone,
      roomId,
      checkInDate: this.reservation.checkInDate,
      checkOutDate: this.reservation.checkOutDate,
      guestCount
    };

    this.saving = true;

    this.http.post<Reservation>(
      `${this.apiUrl}/reservations`,
      payload,
      { headers }
    ).subscribe({
      next: (result) => {
        this.successMessage =
          `Reservation #${result.id} confirmed for Room ${result.roomNumber}. Total: ₹${result.totalPrice}.`;

        this.reservation = {
          guestName: '',
          guestPhone: '',
          roomId: 0,
          checkInDate: this.today,
          checkOutDate: this.addDays(this.today, 1),
          guestCount: 1
        };

        this.selectedReservationGuestId = null;
        this.saving = false;

        this.checkRoomAvailability();
        this.loadReservations();

        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Reservation creation failed:', error);

        if (error.status === 409) {
          this.errorMessage =
            'This room is already booked for those dates. Please choose another room or dates.';
        } else if (error.status === 400) {
          this.errorMessage =
            error.error?.message ??
            'Reservation details were rejected. Check the dates and guest count.';
        } else {
          this.errorMessage =
            error.error?.message ?? 'Reservation failed.';
        }

        this.handleAdminAuthError(error);
        this.saving = false;
        this.cdr.detectChanges();
      }
    });
  }

  cancelReservation(id: number): void {
    const confirmed = window.confirm(
      `Are you sure you want to cancel reservation #${id}?`
    );

    if (!confirmed) return;

    const headers = this.getAdminHeaders();
    if (!headers) return;

    this.errorMessage = '';
    this.successMessage = '';

    this.http.patch<{ message: string; status: string }>(
      `${this.apiUrl}/reservations/${id}/cancel`,
      {},
      { headers }
    ).subscribe({
      next: (result) => {
        this.successMessage = result.message;
        this.loadReservations();
        this.checkRoomAvailability();
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Cancellation failed:', error);
        this.errorMessage =
          error.error?.message ?? 'Could not cancel the reservation.';
        this.handleAdminAuthError(error);
        this.cdr.detectChanges();
      }
    });
  }

  // ---------------- GUEST MANAGEMENT ----------------

  loadGuests(): void {
    const headers = this.getAdminHeaders();
    if (!headers) return;

    this.guestLoading = true;
    this.guestError = '';

    const search = this.guestSearch.trim();

    this.http.get<Guest[]>(
      `${this.apiUrl}/guests`,
      {
        headers,
        params: search ? { search } : {}
      }
    ).subscribe({
      next: (guests) => {
        this.guests = guests;
        this.guestLoading = false;
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to load guests:', error);
        this.guestError = 'Could not load guests.';
        this.guestLoading = false;
        this.handleAdminAuthError(error);
        this.cdr.detectChanges();
      }
    });
  }

  loadReservationGuests(): void {
    const headers = this.getAdminHeaders();
    if (!headers) return;

    this.http.get<Guest[]>(
      `${this.apiUrl}/guests`,
      { headers }
    ).subscribe({
      next: (guests) => {
        this.reservationGuests = guests;
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to load reservation guest list:', error);
        this.handleAdminAuthError(error);
      }
    });
  }

  selectReservationGuest(id: number | null): void {
    this.selectedReservationGuestId = id;
    this.errorMessage = '';
    this.successMessage = '';

    if (id === null) {
      this.reservation.guestName = '';
      this.reservation.guestPhone = '';
      return;
    }

    const guest = this.reservationGuests.find(
      item => item.id === Number(id)
    );

    if (!guest) {
      this.reservation.guestName = '';
      this.reservation.guestPhone = '';
      return;
    }

    this.reservation.guestName = guest.fullName;
    this.reservation.guestPhone = guest.phone;
  }

  searchGuests(): void {
    this.loadGuests();
  }

  createGuest(): void {
    this.guestMessage = '';
    this.guestError = '';

    const fullName = this.guestName.trim();
    const phone = this.guestPhone.trim();
    const email = this.guestEmail.trim();

    if (!fullName || !phone) {
      this.guestError = 'Guest name and phone number are required.';
      return;
    }

    if (fullName.length > 120) {
      this.guestError = 'Guest name cannot exceed 120 characters.';
      return;
    }

    if (phone.length > 20) {
      this.guestError = 'Phone number cannot exceed 20 characters.';
      return;
    }

    if (email.length > 254) {
      this.guestError = 'Email cannot exceed 254 characters.';
      return;
    }

    const headers = this.getAdminHeaders();
    if (!headers) return;

    this.guestSaving = true;

    this.http.post<Guest & { message?: string }>(
      `${this.apiUrl}/guests`,
      {
        fullName,
        phone,
        email: email || null
      },
      { headers }
    ).subscribe({
      next: (guest) => {
        this.guestMessage =
          guest.message ?? 'Guest registered successfully.';

        this.guestName = '';
        this.guestPhone = '';
        this.guestEmail = '';
        this.guestSaving = false;

        this.loadGuests();
        this.loadReservationGuests();
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Guest registration failed:', error);
        this.guestError =
          error.error?.message ?? 'Could not register the guest.';
        this.handleAdminAuthError(error);
        this.guestSaving = false;
        this.cdr.detectChanges();
      }
    });
  }

  viewGuest(id: number): void {
    const headers = this.getAdminHeaders();
    if (!headers) return;

    this.selectedGuest = null;
    this.guestDetailsLoading = true;
    this.guestError = '';

    this.http.get<GuestDetails>(
      `${this.apiUrl}/guests/${id}`,
      { headers }
    ).subscribe({
      next: (guest) => {
        this.selectedGuest = guest;
        this.guestDetailsLoading = false;
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to load guest details:', error);
        this.guestError =
          error.error?.message ?? 'Could not load guest details.';
        this.handleAdminAuthError(error);
        this.guestDetailsLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  closeGuestDetails(): void {
    this.selectedGuest = null;
  }

  // ---------------- SERVICE REQUESTS ----------------

  loadServiceRequests(): void {
    const headers = this.getAdminHeaders();
    if (!headers) return;

    this.serviceRequestLoading = true;

    const params: Record<string, string> = {};

    if (this.serviceRequestStatus) {
      params['status'] = this.serviceRequestStatus;
    }

    if (this.serviceRequestDepartment) {
      params['department'] = this.serviceRequestDepartment;
    }

    this.http.get<ServiceRequest[]>(
      `${this.apiUrl}/service-requests`,
      {
        headers,
        params
      }
    ).subscribe({
      next: (requests) => {
        this.serviceRequests = requests;
        this.serviceRequestLoading = false;
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to load service requests:', error);
        this.serviceRequestLoading = false;
        this.errorMessage = 'Could not load service requests.';
        this.handleAdminAuthError(error);
        this.cdr.detectChanges();
      }
    });
  }

  updateServiceRequestStatus(id: number, status: string): void {
    const headers = this.getAdminHeaders();
    if (!headers) return;

    this.http.patch<{ message: string }>(
      `${this.apiUrl}/service-requests/${id}/status`,
      { status },
      { headers }
    ).subscribe({
      next: () => {
        this.successMessage = `Service request #${id} updated.`;
        this.loadServiceRequests();
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to update service request:', error);
        this.errorMessage =
          error.error?.message ?? 'Could not update service request.';
        this.handleAdminAuthError(error);
        this.cdr.detectChanges();
      }
    });
  }

  // ---------------- AI CHAT ----------------
  // This remains the existing admin AI request contract.
  // The backend AI controller currently uses a different guest-session contract.

  sendAiMessage(): void {
    const message = this.aiUserMessage.trim();

    if (!message || this.aiLoading) return;

    if (message.length > 2000) {
      this.aiError = 'Message must be 2000 characters or fewer.';
      return;
    }

    this.aiMessages.push({
      role: 'user',
      content: message
    });

    this.aiUserMessage = '';
    this.aiLoading = true;
    this.aiError = '';

    this.http.post<{
      answer: string;
      serviceRequestId?: number;
    }>(
      `${this.apiUrl}/ai/chat`,
      { message }
    ).subscribe({
      next: (response) => {
        this.aiMessages.push({
          role: 'assistant',
          content: response.answer
        });

        if (response.serviceRequestId !== undefined) {
          this.loadServiceRequests();
        }

        this.aiLoading = false;
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('AI chat error:', error);
        this.aiError =
          error.error?.message ??
          'The AI assistant is unavailable. Please try again.';
        this.aiLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  // ---------------- GUEST PORTAL LOGIN ----------------

  loginGuest(): void {
    const reservationId = Number(this.guestLoginReservationId);
    const roomNumber = this.guestLoginRoomNumber.trim();
    const phone = this.guestLoginPhone.trim();

    this.guestLoginError = '';
    this.guestLoginMessage = '';

    if (
      !Number.isInteger(reservationId) ||
      reservationId <= 0 ||
      !roomNumber ||
      !phone
    ) {
      this.guestLoginError =
        'Enter your reservation ID, room number and phone number.';
      return;
    }

    this.guestLoginLoading = true;

    this.http.post<{
      message: string;
      token: string;
      expiresAtUtc: string;
      guestName: string;
      reservationId: number;
      roomNumber: string;
    }>(
      `${this.apiUrl}/guest-sessions/verify`,
      { reservationId, roomNumber, phone }
    ).subscribe({
      next: (response) => {
        this.guestSessionToken = response.token;
        this.guestPortal = {
          guestName: response.guestName,
          reservationId: response.reservationId,
          roomNumber: response.roomNumber,
          expiresAtUtc: response.expiresAtUtc
        };

        sessionStorage.setItem(
          'havenGuestSessionToken',
          response.token
        );

        this.guestLoginMessage = 'Guest verification successful.';
        this.guestLoginLoading = false;
        this.cdr.detectChanges();
      },
      error: (error) => {
        this.guestLoginError =
          error.error?.message ??
          'Could not verify the reservation details.';
        this.guestLoginLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  logoutGuest(): void {
    const token = this.guestSessionToken;

    if (!token) {
      this.clearGuestSession();
      return;
    }

    this.http.post<{ message: string }>(
      `${this.apiUrl}/guest-sessions/logout`,
      {},
      { headers: { 'X-Guest-Session': token } }
    ).subscribe({
      next: () => {
        this.clearGuestSession();
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Guest logout request failed:', error);
        this.clearGuestSession();
        this.cdr.detectChanges();
      }
    });
  }

  private clearGuestSession(): void {
    this.guestSessionToken = '';
    this.guestPortal = null;
    this.guestLoginReservationId = null;
    this.guestLoginRoomNumber = '';
    this.guestLoginPhone = '';
    this.guestLoginMessage = '';
    this.guestLoginError = '';

    sessionStorage.removeItem('havenGuestSessionToken');
  }
}
