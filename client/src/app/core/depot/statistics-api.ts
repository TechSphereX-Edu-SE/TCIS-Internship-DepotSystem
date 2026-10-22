import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Throughput, YardInventory } from './depot-models';

// Depot statistics: /api/statistics (all dates are Vietnam dates "yyyy-MM-dd")
@Service()
export class StatisticsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/statistics';

  // Containers in the yard per line operator, split at longStayDays (1..365, API default 10)
  yardInventory(longStayDays: number): Observable<YardInventory> {
    return this.http.get<YardInventory>(`${this.baseUrl}/yard-inventory`, { params: { longStayDays } });
  }

  // Gate-ins and gate-outs per day and line operator; without lineOperatorId every operator is included
  throughput(from: string, to: string, lineOperatorId?: string): Observable<Throughput> {
    const params: Record<string, string> = { from, to };
    if (lineOperatorId) params['lineOperatorId'] = lineOperatorId;
    return this.http.get<Throughput>(`${this.baseUrl}/throughput`, { params });
  }
}
