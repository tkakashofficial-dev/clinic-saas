import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { parseApiError } from '../../core/api/api-error';
import { InvoicesService } from '../../core/api/invoices.service';
import { PatientsService } from '../../core/api/patients.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  InvoiceDto,
  InvoiceStats,
  PagedResult,
  PatientDto,
  PaymentMethod,
} from '../../core/models/api.models';

interface DraftItem {
  description: string;
  quantity: number;
  unitPriceRupees: number | null;
}

/**
 * Patient billing — the front desk's money screen. Create an itemised bill
 * in seconds, collect at the counter, hand over a letterhead PDF. Today's
 * and this month's collections sit on top: the numbers an owner checks daily.
 */
@Component({
  selector: 'app-invoices',
  imports: [DatePipe, DecimalPipe, FormsModule],
  templateUrl: './invoices.html',
  styleUrl: './invoices.scss',
})
export class Invoices {
  private readonly api = inject(InvoicesService);
  private readonly patientsApi = inject(PatientsService);
  readonly auth = inject(AuthService);

  readonly statusFilters = ['All', 'Unpaid', 'Paid', 'Cancelled'];
  readonly payMethods: { key: PaymentMethod; label: string }[] = [
    { key: 'Upi', label: 'UPI' },
    { key: 'Cash', label: 'Cash' },
    { key: 'Card', label: 'Card' },
    { key: 'BankTransfer', label: 'Bank' },
    { key: 'Other', label: 'Other' },
  ];

  readonly loading = signal(true);
  readonly result = signal<PagedResult<InvoiceDto> | null>(null);
  readonly stats = signal<InvoiceStats | null>(null);
  readonly status = signal('All');
  readonly page = signal(1);
  readonly error = signal('');
  readonly notice = signal('');
  readonly busy = signal<string | null>(null);
  readonly payMenuFor = signal<string | null>(null);

  readonly canManage = computed(() => this.auth.hasRole('Admin', 'Receptionist'));

  // ---- create drawer ----
  readonly createOpen = signal(false);
  readonly saving = signal(false);
  readonly formError = signal('');
  readonly selectedPatient = signal<PatientDto | null>(null);
  readonly patientResults = signal<PatientDto[]>([]);
  private readonly patientSearch$ = new Subject<string>();

  draftItems: DraftItem[] = [this.blankItem()];
  draftDiscount: number | null = null;
  draftNotes = '';
  collectNow = true;
  draftMethod: PaymentMethod = 'Upi';

  constructor() {
    this.patientSearch$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) => this.patientsApi.getAll(term, 1, 8)),
        takeUntilDestroyed(),
      )
      .subscribe((result) => this.patientResults.set(result.items));

    this.load();
    if (this.canManage()) this.loadStats();
  }

  private blankItem(): DraftItem {
    return { description: '', quantity: 1, unitPriceRupees: null };
  }

  load(): void {
    this.loading.set(true);
    this.api.getAll({
      status: this.status() === 'All' ? undefined : this.status(),
      page: this.page(),
      pageSize: 10,
    }).subscribe({
      next: (result) => {
        this.result.set(result);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(parseApiError(err).message);
        this.loading.set(false);
      },
    });
  }

  loadStats(): void {
    this.api.getStats().subscribe({
      next: (stats) => this.stats.set(stats),
      error: () => {},
    });
  }

  setStatus(status: string): void {
    this.status.set(status);
    this.page.set(1);
    this.load();
  }

  goToPage(page: number): void {
    this.page.set(page);
    this.load();
  }

  invoiceCode(invoice: InvoiceDto): string {
    return `INV-${String(invoice.invoiceNumber).padStart(6, '0')}`;
  }

  // ---- table actions ----

  togglePayMenu(invoice: InvoiceDto): void {
    this.payMenuFor.update((id) => (id === invoice.id ? null : invoice.id));
  }

  markPaid(invoice: InvoiceDto, method: PaymentMethod): void {
    this.payMenuFor.set(null);
    this.busy.set(invoice.id);
    this.api.markPaid(invoice.id, method).subscribe({
      next: () => {
        this.busy.set(null);
        this.notice.set(`${this.invoiceCode(invoice)} marked paid.`);
        this.load();
        this.loadStats();
      },
      error: (err) => {
        this.busy.set(null);
        this.error.set(parseApiError(err).message);
      },
    });
  }

  cancelInvoice(invoice: InvoiceDto): void {
    if (!confirm(`Cancel ${this.invoiceCode(invoice)}? This cannot be undone.`)) return;
    this.busy.set(invoice.id);
    this.api.cancel(invoice.id).subscribe({
      next: () => {
        this.busy.set(null);
        this.load();
        this.loadStats();
      },
      error: (err) => {
        this.busy.set(null);
        this.error.set(parseApiError(err).message);
      },
    });
  }

  downloadPdf(invoice: InvoiceDto): void {
    this.api.pdf(invoice.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank');
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
    });
  }

  // ---- create drawer ----

  openCreate(): void {
    this.selectedPatient.set(null);
    this.loadRecentPatients();   // dropdown works without remembering names
    this.draftItems = [this.blankItem()];
    this.draftDiscount = null;
    this.draftNotes = '';
    this.collectNow = true;
    this.draftMethod = 'Upi';
    this.formError.set('');
    this.createOpen.set(true);
  }

  /** Empty field shows the most recent patients; typing filters everyone. */
  private loadRecentPatients(): void {
    this.patientsApi.getAll('', 1, 8).subscribe({
      next: (result) => this.patientResults.set(result.items),
      error: () => {},
    });
  }

  searchPatients(term: string): void {
    const trimmed = term.trim();
    if (trimmed.length >= 2) this.patientSearch$.next(trimmed);
    else if (trimmed.length === 0) this.loadRecentPatients();
  }

  addItem(): void {
    this.draftItems.push(this.blankItem());
  }

  removeItem(index: number): void {
    if (this.draftItems.length > 1) this.draftItems.splice(index, 1);
  }

  lineTotal(item: DraftItem): number {
    return (item.quantity || 0) * (item.unitPriceRupees || 0);
  }

  get subtotal(): number {
    return this.draftItems.reduce((sum, item) => sum + this.lineTotal(item), 0);
  }

  get total(): number {
    return Math.max(0, this.subtotal - (this.draftDiscount || 0));
  }

  get draftValid(): boolean {
    return !!this.selectedPatient()
      && this.draftItems.some((i) => i.description.trim() && (i.unitPriceRupees || 0) > 0)
      && (this.draftDiscount || 0) <= this.subtotal;
  }

  save(): void {
    const patient = this.selectedPatient();
    if (!patient) return;

    this.saving.set(true);
    this.formError.set('');

    this.api.create({
      patientId: patient.id,
      items: this.draftItems
        .filter((i) => i.description.trim() && (i.unitPriceRupees ?? 0) >= 0)
        .map((i) => ({
          description: i.description.trim(),
          quantity: i.quantity || 1,
          unitPriceRupees: i.unitPriceRupees || 0,
        })),
      discountRupees: this.draftDiscount || 0,
      notes: this.draftNotes.trim() || null,
      markPaid: this.collectNow,
      paymentMethod: this.collectNow ? this.draftMethod : null,
    }).subscribe({
      next: (invoice) => {
        this.saving.set(false);
        this.createOpen.set(false);
        this.notice.set(
          `${this.invoiceCode(invoice)} created${this.collectNow ? ' and marked paid' : ''} — ` +
          `₹${invoice.totalRupees.toLocaleString('en-IN')}.`);
        this.load();
        this.loadStats();
        this.downloadPdf(invoice);   // the receipt opens, ready to print
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(parseApiError(err).message);
      },
    });
  }
}
