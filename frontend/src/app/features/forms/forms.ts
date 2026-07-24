import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { parseApiError } from '../../core/api/api-error';
import { FormsService } from '../../core/api/forms.service';
import { SettingsService } from '../../core/api/settings.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  FormSectionKind,
  FormSectionTemplate,
  IntakeFormSection,
  IntakeTemplate,
} from '../../core/models/api.models';

interface TemplateCard {
  key: IntakeTemplate;
  emoji: string;
  title: string;
  blurb: string;
}

/**
 * Forms — the intake form's home. Admins pick the clinic's default template,
 * add their OWN sections (form builder v1: writing boxes, labelled lines,
 * checklists) and preview the exact PDF with sample data. Doctors and
 * reception just print it from a patient's record.
 */
@Component({
  selector: 'app-forms',
  imports: [FormsModule],
  templateUrl: './forms.html',
  styleUrl: './forms.scss',
})
export class Forms {
  private readonly api = inject(FormsService);
  private readonly settingsApi = inject(SettingsService);
  readonly auth = inject(AuthService);

  readonly templates: TemplateCard[] = [
    {
      key: 'dental', emoji: '🦷', title: 'Dental clinic',
      blurb: 'Disease checklist, dental history, oral health status, intra/extra oral examination, ortho findings, consent.',
    },
    {
      key: 'general', emoji: '🩺', title: 'General clinic',
      blurb: 'Vitals strip, disease checklist, medical/surgical history, general & systemic examination, consent.',
    },
  ];

  readonly isAdmin = computed(() => this.auth.hasRole('Admin'));
  readonly defaultTemplate = computed(
    () => this.settingsApi.settings()?.defaultIntakeTemplate ?? 'dental');

  readonly sections = signal<IntakeFormSection[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly notice = signal('');
  readonly previewing = signal<string | null>(null);
  readonly settingDefault = signal(false);

  // ---- add-section drawer ----
  readonly addOpen = signal(false);
  readonly saving = signal(false);
  readonly formError = signal('');
  readonly kinds: { key: FormSectionKind; emoji: string; label: string; hint: string }[] = [
    { key: 'box', emoji: '📝', label: 'Writing box', hint: 'A titled area with space to write' },
    { key: 'lines', emoji: '➖', label: 'Labelled lines', hint: 'Each item gets its own line to fill' },
    { key: 'checklist', emoji: '☑️', label: 'Checklist', hint: 'Tick-box items, two columns' },
  ];
  newKind: FormSectionKind = 'box';
  newTitle = '';
  newTemplate: FormSectionTemplate = 'both';
  newItemsText = '';

  constructor() {
    if (!this.settingsApi.settings()) {
      this.settingsApi.get().subscribe({ error: () => {} });
    }
    this.load();
  }

  load(): void {
    this.error.set('');   // otherwise a past failure lingers forever
    this.api.getSections().subscribe({
      next: (sections) => {
        this.sections.set(sections);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(parseApiError(err).message);
        this.loading.set(false);
      },
    });
  }

  kindMeta(kind: FormSectionKind) {
    return this.kinds.find((k) => k.key === kind) ?? this.kinds[0];
  }

  preview(template: IntakeTemplate): void {
    this.previewing.set(template);
    this.error.set('');
    this.api.preview(template).subscribe({
      next: (blob) => {
        // Anchor-download, not window.open — iOS Safari blocks popups opened
        // from an async callback, so the preview never appeared on iPhone
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `intake-${template}-preview.pdf`;
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
        this.previewing.set(null);
      },
      error: (err) => {
        this.error.set(parseApiError(err).message);
        this.previewing.set(null);
      },
    });
  }

  setDefault(template: IntakeTemplate): void {
    const settings = this.settingsApi.settings();
    if (!settings || settings.defaultIntakeTemplate === template) return;

    this.error.set('');
    this.settingDefault.set(true);
    this.settingsApi.update({ ...settings, defaultIntakeTemplate: template }).subscribe({
      next: () => {
        this.settingDefault.set(false);
        this.notice.set(`${template === 'dental' ? 'Dental' : 'General'} is now your clinic's default form.`);
      },
      error: (err) => {
        this.settingDefault.set(false);
        this.error.set(parseApiError(err).message);
      },
    });
  }

  openAdd(): void {
    this.newKind = 'box';
    this.newTitle = '';
    this.newTemplate = 'both';
    this.newItemsText = '';
    this.formError.set('');
    this.addOpen.set(true);
  }

  addSection(): void {
    const items = this.newItemsText
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0);

    this.saving.set(true);
    this.formError.set('');

    this.api.createSection({
      kind: this.newKind,
      title: this.newTitle.trim(),
      template: this.newTemplate,
      items,
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.addOpen.set(false);
        this.notice.set('Section added — it now prints on your intake form.');
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(parseApiError(err).message);
      },
    });
  }

  move(section: IntakeFormSection, direction: 1 | -1): void {
    this.error.set('');
    this.api.moveSection(section.id, direction).subscribe({
      next: (sections) => this.sections.set(sections),
      error: (err) => this.error.set(parseApiError(err).message),
    });
  }

  remove(section: IntakeFormSection): void {
    if (!confirm(`Remove "${section.title}" from your form?`)) return;
    this.error.set('');
    this.api.deleteSection(section.id).subscribe({
      next: () => {
        this.notice.set(`"${section.title}" removed.`);
        this.load();
      },
      error: (err) => this.error.set(parseApiError(err).message),
    });
  }
}
