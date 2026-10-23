import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { map, startWith } from 'rxjs';
import { ApiError, toApiError } from '../../core/api/api-error';
import {
  Block,
  CATEGORIES,
  CATEGORY_HINT,
  CATEGORY_LABEL,
  ContainerCategory,
  categoriesText,
} from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { LIST_PAGE_SIZE, Pager } from '../../shared/pager/pager';
import { ToastService } from '../../shared/toast/toast-service';

// Same as Block.MinTiers: tiers stack "from 2 to many" (spec section 1)
const MIN_TIERS = 2;

// Whole number at least `min`, for the bay / row / tier fields
const sizeValidators = (min: number) => [Validators.required, Validators.min(min), Validators.pattern(/^\d+$/)];

/*
  What the form on the right does:
    - create: a new block (physical or virtual)
    - edit: code, description and accepted types of an existing block (feedback 9, both kinds)
    - resize: bays / rows / tiers of a physical block
  A block never switches between physical and virtual: its visits are stored in that positioning mode.
*/
type FormMode = { kind: 'create' } | { kind: 'edit'; block: Block } | { kind: 'resize'; block: Block };

/*
  Spec section 1: yard blocks. A physical block has bays x rows x tiers (bay = 20ft unit,
  odd bays hold 20ft, even bays 40ft); a virtual block (e.g. WAIT for repair) has no positions.
  Each block lists the container types it takes (feedback 3); gate-in and move check that list.
  Feedback 9: a block with no container in it can be deleted; one with history is archived instead
  (kept for lookups, listed after the active ones, no longer offered for new positions).
*/
@Component({
  selector: 'app-blocks-page',
  imports: [ReactiveFormsModule, Pager],
  templateUrl: './blocks-page.html',
})
export class BlocksPage {
  private readonly api = inject(MasterDataApi);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder).nonNullable;

  protected readonly minTiers = MIN_TIERS;
  protected readonly categories = CATEGORIES;
  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly categoryHint = CATEGORY_HINT;
  protected readonly categoriesText = categoriesText;

  /*
    Create and edit share this form (same rules as CreateBlockValidator / UpdateBlockValidator).
    The sizes are disabled (and sent as null) for a virtual block; in edit mode the type and the
    sizes are not part of the request at all.
  */
  protected readonly detailsForm = this.fb.group({
    code: ['', [Validators.required, Validators.pattern(/^[A-Za-z0-9]{1,10}$/)]],
    description: ['', Validators.maxLength(200)],
    isVirtual: [false],
    maxBays: new FormControl<number | null>(null, sizeValidators(1)),
    maxRows: new FormControl<number | null>(null, sizeValidators(1)),
    maxTiers: new FormControl<number | null>(null, sizeValidators(MIN_TIERS)),
    // Feedback 3: the types this block takes; at least one
    allowedCategories: new FormControl<ContainerCategory[]>([], {
      nonNullable: true,
      validators: (control) => (control.value.length === 0 ? { required: true } : null),
    }),
  });

  // Same rules as ResizeBlockValidator
  protected readonly resizeForm = this.fb.group({
    maxBays: [1, sizeValidators(1)],
    maxRows: [1, sizeValidators(1)],
    maxTiers: [MIN_TIERS, sizeValidators(MIN_TIERS)],
  });

  protected readonly items = signal<Block[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly listError = signal<string | null>(null);
  protected readonly mode = signal<FormMode>({ kind: 'create' });
  protected readonly submitting = signal(false);
  protected readonly submitted = signal(false);
  protected readonly apiError = signal<ApiError | null>(null);

  // Block shown as "being edited" in the list (edit or resize)
  protected readonly selectedId = computed(() => {
    const mode = this.mode();
    return mode.kind === 'create' ? null : mode.block.id;
  });

  protected readonly resizing = computed(() => {
    const mode = this.mode();
    return mode.kind === 'resize' ? mode.block : null;
  });

  protected readonly editing = computed(() => {
    const mode = this.mode();
    return mode.kind === 'edit' ? mode.block : null;
  });

  protected readonly isVirtual = toSignal(
    this.detailsForm.controls.isVirtual.valueChanges.pipe(startWith(false)),
    { initialValue: false },
  );

  private readonly resizeValue = toSignal(
    this.resizeForm.valueChanges.pipe(
      startWith(null),
      map(() => this.resizeForm.getRawValue()),
    ),
    { initialValue: this.resizeForm.getRawValue() },
  );

  private readonly categoriesValue = toSignal(
    this.detailsForm.controls.allowedCategories.valueChanges.pipe(startWith([] as ContainerCategory[])),
    { initialValue: [] as ContainerCategory[] },
  );

  // Shrinking can fail (409) when a container stands outside the new size; say so before trying
  protected readonly shrinking = computed(() => {
    const block = this.resizing();
    const v = this.resizeValue();
    if (!block || block.containersInYard === 0) return false;
    return v.maxBays < block.maxBays! || v.maxRows < block.maxRows! || v.maxTiers < block.maxTiers!;
  });

  // Removing a type can fail (409) while a container of that type is in the block; warn before saving
  protected readonly removingTypes = computed(() => {
    const block = this.editing();
    if (!block || block.containersInYard === 0) return false;
    const next = this.categoriesValue();
    return block.allowedCategories.some((c) => !next.includes(c));
  });

  constructor() {
    this.load(1);
    // Virtual block: no sizes at all (CreateBlockValidator requires them to be null)
    this.detailsForm.controls.isVirtual.valueChanges.pipe(takeUntilDestroyed()).subscribe((virtual) => {
      this.setSizesEnabled(!virtual);
    });
  }

  protected load(page: number): void {
    this.api.blockPage(page, LIST_PAGE_SIZE).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.totalPages.set(Math.max(1, result.totalPages));
        this.page.set(page);
        this.listError.set(null);
      },
      error: (err: unknown) => this.listError.set(toApiError(err).message),
    });
  }

  protected sizeText(block: Block): string {
    return block.isVirtual ? 'Không quản lý bay/row/tier' : `${block.maxBays} bay × ${block.maxRows} row × ${block.maxTiers} tier`;
  }

  // Code, description and types; the kind (physical/virtual) and the sizes stay as they are
  protected startEdit(block: Block): void {
    this.mode.set({ kind: 'edit', block });
    this.detailsForm.reset({
      code: block.code,
      description: block.description ?? '',
      isVirtual: block.isVirtual,
      allowedCategories: [...block.allowedCategories],
    });
    this.setSizesEnabled(false);
    this.detailsForm.controls.isVirtual.disable({ emitEvent: false });
    this.submitted.set(false);
    this.apiError.set(null);
  }

  // A virtual block has nothing to resize, so only physical blocks get the button
  protected startResize(block: Block): void {
    this.mode.set({ kind: 'resize', block });
    this.resizeForm.reset({ maxBays: block.maxBays!, maxRows: block.maxRows!, maxTiers: block.maxTiers! });
    this.submitted.set(false);
    this.apiError.set(null);
  }

  // Back to "create" mode
  protected cancel(): void {
    this.mode.set({ kind: 'create' });
    this.detailsForm.controls.isVirtual.enable({ emitEvent: false });
    this.detailsForm.reset({ isVirtual: false, allowedCategories: [] });
    this.setSizesEnabled(true);
    this.submitted.set(false);
    this.apiError.set(null);
  }

  /*
    Feedback 9: confirm first. The API decides between delete and archive (history), and refuses
    while a container is in the block; containers are never moved or removed automatically.
  */
  protected remove(block: Block): void {
    const question =
      `Xoá block ${block.code}?\n\n` +
      'Nếu block đã từng có container, hệ thống sẽ lưu trữ block (giữ để tra cứu lịch sử, ' +
      'không còn hiện khi chọn vị trí mới) thay vì xoá hẳn.';
    if (!confirm(question)) return;

    this.api.deleteBlock(block.id).subscribe({
      next: (result) => {
        this.toast.success(
          result.archived ? `Đã lưu trữ block ${result.code} (có lịch sử)` : `Đã xoá block ${result.code}`,
        );
        if (this.selectedId() === block.id) this.cancel();
        this.load(this.page());
      },
      error: (err: unknown) => this.toast.error(`Không xoá được block ${block.code}: ${toApiError(err).message}`),
    });
  }

  protected submit(): void {
    this.submitted.set(true);
    this.apiError.set(null);
    const mode = this.mode();
    if (mode.kind === 'resize') this.saveResize(mode.block);
    else if (mode.kind === 'edit') this.saveEdit(mode.block);
    else this.saveCreate();
  }

  // Ticks or unticks one type; the list is kept in enum order so the request is stable
  protected toggleCategory(category: ContainerCategory, checked: boolean): void {
    const control = this.detailsForm.controls.allowedCategories;
    const next = checked ? [...control.value, category] : control.value.filter((c) => c !== category);
    control.setValue(CATEGORIES.filter((c) => next.includes(c)));
    control.markAsTouched();
  }

  protected invalid(
    form: 'details' | 'resize',
    name: 'code' | 'description' | 'maxBays' | 'maxRows' | 'maxTiers' | 'allowedCategories',
  ): boolean {
    // Typed as AbstractControl: the two forms have different value types
    const control: AbstractControl | null = form === 'details' ? this.detailsForm.get(name) : this.resizeForm.get(name);
    return !!control && control.enabled && control.invalid && (control.touched || this.submitted());
  }

  private setSizesEnabled(enabled: boolean): void {
    const sizes = [this.detailsForm.controls.maxBays, this.detailsForm.controls.maxRows, this.detailsForm.controls.maxTiers];
    sizes.forEach((control) => {
      if (enabled) {
        control.enable();
      } else {
        control.setValue(null);
        control.disable();
      }
    });
  }

  private saveCreate(): void {
    this.detailsForm.markAllAsTouched();
    if (this.detailsForm.invalid) return;

    const v = this.detailsForm.getRawValue();
    const code = v.code.trim().toUpperCase();
    this.submitting.set(true);
    this.api
      .createBlock({
        code,
        description: v.description.trim() || null,
        isVirtual: v.isVirtual,
        maxBays: v.isVirtual ? null : Number(v.maxBays),
        maxRows: v.isVirtual ? null : Number(v.maxRows),
        maxTiers: v.isVirtual ? null : Number(v.maxTiers),
        allowedCategories: v.allowedCategories,
      })
      .subscribe({
        next: () => this.done(`Đã thêm block ${code}`),
        error: (err: unknown) => this.failed(err),
      });
  }

  private saveEdit(block: Block): void {
    this.detailsForm.markAllAsTouched();
    if (this.detailsForm.invalid) return;

    const v = this.detailsForm.getRawValue();
    const code = v.code.trim().toUpperCase();
    this.submitting.set(true);
    this.api
      .updateBlock(block.id, {
        code,
        description: v.description.trim() || null,
        allowedCategories: v.allowedCategories,
      })
      .subscribe({
        next: () => this.done(`Đã lưu block ${code}`),
        error: (err: unknown) => this.failed(err),
      });
  }

  private saveResize(block: Block): void {
    this.resizeForm.markAllAsTouched();
    if (this.resizeForm.invalid) return;

    const v = this.resizeForm.getRawValue();
    this.submitting.set(true);
    this.api
      .resizeBlock(block.id, { maxBays: Number(v.maxBays), maxRows: Number(v.maxRows), maxTiers: Number(v.maxTiers) })
      .subscribe({
        next: () => this.done(`Đã đổi kích thước block ${block.code}`),
        error: (err: unknown) => this.failed(err),
      });
  }

  private done(message: string): void {
    this.toast.success(message);
    this.cancel();
    this.load(this.page());
    this.submitting.set(false);
  }

  private failed(err: unknown): void {
    this.apiError.set(toApiError(err));
    this.submitting.set(false);
  }
}
