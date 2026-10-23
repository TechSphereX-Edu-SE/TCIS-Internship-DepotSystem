import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Observable, concatMap } from 'rxjs';
import { ApiError, toApiError } from '../../core/api/api-error';
import {
  COST_PAYERS,
  COST_PAYER_LABEL,
  COST_PAYER_STATUSES,
  COST_PAYER_STATUS_LABEL,
  CostPayer,
  CostPayerStatus,
  ESTIMATE_CURRENCIES,
  ESTIMATE_DECISIONS,
  ESTIMATE_DECISION_LABEL,
  ESTIMATE_MAX_AMOUNT,
  ESTIMATE_MAX_LINES,
  ESTIMATE_STATUS_LABEL,
  RepairCaseDetail,
  RepairEstimate,
  RepairEstimateDecision,
  RepairEstimateLine,
  RepairEstimateLineInput,
  RepairEstimateStatus,
  formatDateTime,
  formatMoney,
  payerText,
} from '../../core/depot/depot-models';
import { RepairApi } from '../../core/depot/repair-api';
import { ToastService } from '../../shared/toast/toast-service';

// Draft = grey (still editable), waiting = amber, approved = green, refused = red
const ESTIMATE_CHIP: Record<RepairEstimateStatus, string> = {
  Draft: 'chip-grey',
  Submitted: 'chip-amber',
  Approved: 'chip-green',
  Rejected: 'chip-red',
  ChangesRequested: 'chip-amber',
};

const PAYER_STATUS_CHIP: Record<CostPayerStatus, string> = {
  Expected: 'chip-grey',
  Agreed: 'chip-green',
  Disputed: 'chip-red',
};

// "Bên khác" needs the name of the party (same rule as RepairEstimate.PayerNameRequired)
function payerNameRequired(group: AbstractControl): ValidationErrors | null {
  const payer = group.get('payer')?.value as CostPayer;
  const name = (group.get('payerName')?.value as string).trim();
  return payer === 'Other' && !name ? { payerName: true } : null;
}

/*
  Feedback 4 (chi phí sửa chữa) on the repair page: every estimate version of the case, newest first.
    - Draft: the item table is edited here (damage, repair method, amount, who pays) and sent for approval.
    - Submitted: the answer of the approving party is recorded (approve / changes / reject). Approving the
      first version is what approves the repair; there is no separate "Duyệt phương án" button any more.
    - Any version: who pays one item and how far that is settled (expected, agreed, disputed with a reason).
  A refused version stays as it was; "Lập phiên bản mới" copies the latest one so only the changes are made.
  The parent reloads the case after each change (changed output), so the panel always shows the API state.
*/
@Component({
  selector: 'app-repair-estimate-panel',
  imports: [ReactiveFormsModule],
  templateUrl: './repair-estimate-panel.html',
  styleUrl: './repair-estimate-panel.css',
})
export class RepairEstimatePanel {
  private readonly api = inject(RepairApi);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder).nonNullable;

  readonly detail = input.required<RepairCaseDetail>();
  readonly changed = output<void>();

  protected readonly statusLabel = ESTIMATE_STATUS_LABEL;
  protected readonly statusChip = ESTIMATE_CHIP;
  protected readonly payerLabel = COST_PAYER_LABEL;
  protected readonly payerStatusLabel = COST_PAYER_STATUS_LABEL;
  protected readonly payerStatusChip = PAYER_STATUS_CHIP;
  protected readonly decisionLabel = ESTIMATE_DECISION_LABEL;
  protected readonly payers = COST_PAYERS;
  protected readonly payerStatuses = COST_PAYER_STATUSES;
  protected readonly decisions = ESTIMATE_DECISIONS;
  protected readonly currencies = ESTIMATE_CURRENCIES;
  protected readonly maxLines = ESTIMATE_MAX_LINES;
  protected readonly formatDateTime = formatDateTime;
  protected readonly formatMoney = formatMoney;
  protected readonly payerText = payerText;

  protected readonly busy = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  // Older versions are folded; the newest is always open
  protected readonly unfolded = signal<Record<string, boolean>>({});

  protected readonly estimates = computed(() => this.detail().estimates);
  protected readonly latest = computed(() => this.estimates()[0] ?? null);
  protected readonly isCaseOpen = computed(() => this.detail().status !== 'Passed');

  // New version: currency of the latest one by default (VND for the first)
  protected readonly createForm = this.fb.group({ currency: ['VND'] });

  // ---------- draft table ----------
  protected readonly draftForm = this.fb.group({
    currency: ['VND', Validators.required],
    note: ['', Validators.maxLength(1000)],
    evidence: ['', Validators.maxLength(1000)],
    lines: this.fb.array([this.newLine()]),
  });
  protected readonly draftSubmitted = signal(false);
  private draftId: string | null = null;

  // ---------- decision on a submitted version ----------
  protected readonly decisionForm = this.fb.group({
    decision: ['Approve' as RepairEstimateDecision],
    approverParty: ['', Validators.maxLength(200)],
    approverName: ['', Validators.maxLength(200)],
    approvalReference: ['', Validators.maxLength(200)],
    note: ['', Validators.maxLength(1000)],
  });
  protected readonly decisionSubmitted = signal(false);

  // ---------- payer of one item (after submit) ----------
  protected readonly editingLineId = signal<string | null>(null);
  protected readonly payerForm = this.fb.group(
    {
      payer: ['LineOperator' as CostPayer],
      payerName: ['', Validators.maxLength(200)],
      status: ['Expected' as CostPayerStatus],
      note: ['', Validators.maxLength(500)],
    },
    { validators: payerNameRequired },
  );
  protected readonly payerSubmitted = signal(false);

  constructor() {
    /*
      The draft table follows the API: rebuilt when another draft is shown, or when the form has no
      unsaved edits (a reload after a photo upload must not wipe what the user is typing).
    */
    effect(() => {
      const draft = this.estimates().find((e) => e.status === 'Draft') ?? null;
      const latest = this.latest();
      this.createForm.controls.currency.setValue(latest?.currency ?? 'VND');
      if (!draft) {
        this.draftId = null;
        return;
      }
      if (draft.id !== this.draftId || this.draftForm.pristine) this.loadDraft(draft);
    });

    // The approving party is usually the line operator of the visit: offered by default
    effect(() => {
      const submitted = this.latest()?.status === 'Submitted';
      if (submitted && this.decisionForm.pristine) {
        this.decisionForm.reset({ decision: 'Approve', approverParty: this.detail().operatorCode ?? '' });
      }
    });
  }

  protected isUnfolded(estimate: RepairEstimate): boolean {
    return estimate.id === this.latest()?.id || !!this.unfolded()[estimate.id];
  }

  protected toggle(estimate: RepairEstimate): void {
    this.unfolded.update((all) => ({ ...all, [estimate.id]: !all[estimate.id] }));
  }

  // ---------- create ----------

  protected create(): void {
    const latest = this.latest();
    const currency = this.createForm.controls.currency.value;
    this.run(this.api.createEstimate(this.detail().id, currency, null, null), latest ? `Đã lập phiên bản v${latest.version + 1}` : 'Đã lập dự toán v1');
  }

  // ---------- draft ----------

  protected get lineControls() {
    return this.draftForm.controls.lines.controls;
  }

  protected addLine(): void {
    if (this.draftForm.controls.lines.length >= ESTIMATE_MAX_LINES) return;
    this.draftForm.controls.lines.push(this.newLine());
    this.draftForm.markAsDirty();
  }

  protected removeLine(index: number): void {
    this.draftForm.controls.lines.removeAt(index);
    this.draftForm.markAsDirty();
  }

  // Picking a reported damage fills the description when it is still empty
  protected findingPicked(index: number): void {
    const line = this.draftForm.controls.lines.at(index);
    const finding = this.detail().findings.find((f) => f.id === line.controls.findingId.value);
    if (finding && !line.controls.damageDescription.value.trim()) {
      line.controls.damageDescription.setValue(finding.description.slice(0, 500));
    }
  }

  // Live total of the table (shown under the items)
  protected draftTotal(): number {
    return this.draftForm.controls.lines.getRawValue().reduce((sum, l) => sum + (Number(l.amount) || 0), 0);
  }

  protected lineInvalid(index: number, name: 'damageDescription' | 'repairMethod' | 'amount' | 'payerName'): boolean {
    const line = this.draftForm.controls.lines.at(index);
    const shown = line.touched || this.draftSubmitted();
    if (name === 'payerName') return shown && line.hasError('payerName');
    return shown && line.controls[name].invalid;
  }

  protected saveDraft(draft: RepairEstimate): void {
    if (!this.draftReady()) return;
    this.run(this.api.updateEstimate(draft.id, this.draftRequest()), `Đã lưu nháp v${draft.version}`, true);
  }

  // Saves first, so what is sent is exactly what is on screen
  protected submitDraft(draft: RepairEstimate): void {
    if (!this.draftReady()) return;
    if (this.draftForm.controls.lines.length === 0) {
      this.localError('RepairEstimate.NoLines', 'Thêm ít nhất một hạng mục trước khi gửi duyệt.');
      return;
    }
    const save$ = this.api.updateEstimate(draft.id, this.draftRequest());
    this.run(save$.pipe(concatMap(() => this.api.submitEstimate(draft.id))), `Đã gửi duyệt v${draft.version}`, true);
  }

  // ---------- decision ----------

  protected decisionNeedsParty(): boolean {
    return this.decisionForm.controls.decision.value === 'Approve';
  }

  protected decisionInvalid(name: 'approverParty' | 'note'): boolean {
    if (!this.decisionSubmitted()) return false;
    const v = this.decisionForm.getRawValue();
    if (name === 'approverParty') return v.decision === 'Approve' && !v.approverParty.trim();
    return v.decision !== 'Approve' && !v.note.trim();
  }

  protected decide(estimate: RepairEstimate): void {
    this.decisionSubmitted.set(true);
    if (this.decisionInvalid('approverParty') || this.decisionInvalid('note') || this.decisionForm.invalid) return;

    const v = this.decisionForm.getRawValue();
    const request = {
      decision: v.decision,
      approverParty: v.approverParty.trim() || null,
      approverName: v.approverName.trim() || null,
      approvalReference: v.approvalReference.trim() || null,
      note: v.note.trim() || null,
    };
    const done =
      v.decision === 'Approve'
        ? `v${estimate.version} đã duyệt` + (this.detail().status === 'AwaitingEstimateApproval' ? ', hồ sơ chuyển sang Đã duyệt sửa' : '')
        : `v${estimate.version}: ${ESTIMATE_DECISION_LABEL[v.decision].toLowerCase()}`;
    this.run(this.api.decideEstimate(estimate.id, request), done, false, () => {
      this.decisionForm.reset();
      this.decisionSubmitted.set(false);
    });
  }

  // ---------- payer of one item ----------

  protected editPayer(line: RepairEstimateLine): void {
    this.editingLineId.set(line.id);
    this.payerSubmitted.set(false);
    this.payerForm.reset({
      payer: line.payer,
      payerName: line.payerName ?? '',
      status: line.payerStatus,
      note: line.payerNote ?? '',
    });
  }

  protected cancelPayer(): void {
    this.editingLineId.set(null);
  }

  protected payerInvalid(name: 'payerName' | 'note'): boolean {
    if (!this.payerSubmitted()) return false;
    const v = this.payerForm.getRawValue();
    if (name === 'payerName') return this.payerForm.hasError('payerName');
    return v.status === 'Disputed' && !v.note.trim();
  }

  protected savePayer(estimate: RepairEstimate, line: RepairEstimateLine): void {
    this.payerSubmitted.set(true);
    const v = this.payerForm.getRawValue();
    if (this.payerInvalid('payerName') || this.payerInvalid('note') || this.payerForm.invalid) return;
    if (v.status === 'Agreed' && v.payer === 'Undetermined') {
      this.localError('RepairEstimate.PayerUndetermined', 'Chọn bên chịu phí trước khi đánh dấu đã thống nhất.');
      return;
    }
    const request = { payer: v.payer, payerName: v.payerName.trim() || null, status: v.status, note: v.note.trim() || null };
    this.run(this.api.setLinePayer(estimate.id, line.id, request), `Đã cập nhật bên chịu phí hạng mục ${line.position}`, false, () =>
      this.editingLineId.set(null),
    );
  }

  protected findingText(findingId: string | null): string {
    if (!findingId) return '';
    return this.detail().findings.find((f) => f.id === findingId)?.description ?? '';
  }

  // ---------- helpers ----------

  private newLine(line?: RepairEstimateLine) {
    return this.fb.group(
      {
        findingId: [line?.findingId ?? ''],
        damageDescription: [line?.damageDescription ?? '', [Validators.required, Validators.maxLength(500)]],
        repairMethod: [line?.repairMethod ?? '', [Validators.required, Validators.maxLength(500)]],
        amount: [line?.amount ?? (null as number | null), [Validators.required, Validators.min(0), Validators.max(ESTIMATE_MAX_AMOUNT)]],
        payer: [line?.payer ?? ('LineOperator' as CostPayer)],
        payerName: [line?.payerName ?? '', Validators.maxLength(200)],
      },
      { validators: payerNameRequired },
    );
  }

  private loadDraft(draft: RepairEstimate): void {
    this.draftId = draft.id;
    this.draftSubmitted.set(false);
    this.draftForm.controls.lines.clear();
    // A new first version starts with one empty row; a copied version with the items of the previous one
    const lines = draft.lines.length > 0 ? draft.lines : [undefined];
    lines.forEach((l) => this.draftForm.controls.lines.push(this.newLine(l)));
    this.draftForm.patchValue({ currency: draft.currency, note: draft.note ?? '', evidence: draft.evidence ?? '' });
    this.draftForm.markAsPristine();
    this.draftForm.markAsUntouched();
  }

  private draftReady(): boolean {
    this.draftSubmitted.set(true);
    this.draftForm.markAllAsTouched();
    return this.draftForm.valid && !this.busy();
  }

  private draftRequest() {
    const v = this.draftForm.getRawValue();
    const lines: RepairEstimateLineInput[] = v.lines.map((l) => ({
      findingId: l.findingId || null,
      damageDescription: l.damageDescription.trim(),
      repairMethod: l.repairMethod.trim(),
      amount: Number(l.amount),
      payer: l.payer,
      payerName: l.payerName.trim() || null,
    }));
    return { currency: v.currency, note: v.note.trim() || null, evidence: v.evidence.trim() || null, lines };
  }

  // Same rule the API applies, shown before calling it (same box as an API error)
  private localError(code: string, message: string): void {
    this.error.set({ status: 400, code, message, detail: null, fieldErrors: {} });
  }

  // One call at a time; after success the parent reloads the case (and so this panel)
  private run(call: Observable<unknown>, success: string, resetDraft = false, after?: () => void): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    call.subscribe({
      next: () => undefined,
      complete: () => {
        this.busy.set(false);
        if (resetDraft) this.draftForm.markAsPristine();
        after?.();
        this.toast.success(`${this.detail().containerNumber}: ${success}`);
        this.changed.emit();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(toApiError(err));
      },
    });
  }
}
