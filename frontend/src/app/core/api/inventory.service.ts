import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { InventoryItemDto, SaveInventoryItemRequest } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class InventoryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/inventory`;

  getAll(search?: string): Observable<InventoryItemDto[]> {
    return this.http.get<InventoryItemDto[]>(this.baseUrl, {
      params: search ? { search } : {},
    });
  }

  create(request: SaveInventoryItemRequest): Observable<InventoryItemDto> {
    return this.http.post<InventoryItemDto>(this.baseUrl, request);
  }

  update(id: string, request: SaveInventoryItemRequest): Observable<InventoryItemDto> {
    return this.http.put<InventoryItemDto>(`${this.baseUrl}/${id}`, request);
  }

  /** delta > 0 = stock in (purchase), delta < 0 = stock out (dispensed/damaged). */
  adjustStock(id: string, delta: number): Observable<InventoryItemDto> {
    return this.http.post<InventoryItemDto>(`${this.baseUrl}/${id}/adjust-stock`, { delta });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Medicine-name autocomplete for the prescription form (min 2 chars). */
  suggestMedicines(query: string): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/suggest`, { params: { query } });
  }
}
