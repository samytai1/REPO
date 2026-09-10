import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import Aura from '@primeuix/themes/aura';
import { providePrimeNG } from 'primeng/config';
import { ConfirmationService, MessageService } from 'primeng/api';

import { authErrorInterceptor } from '@core/interceptors/auth-error.interceptor';
import { authTokenInterceptor } from '@core/interceptors/auth-token.interceptor';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    // Order matters: the token is attached on the way out, the 401 is caught on the way back.
    provideHttpClient(withFetch(), withInterceptors([authTokenInterceptor, authErrorInterceptor])),
    provideAnimationsAsync(),
    providePrimeNG({
      theme: {
        preset: Aura,
        options: {
          darkModeSelector: '.app-dark',
          cssLayer: {
            name: 'primeng',
            order: 'theme, base, primeng',
          },
        },
      },
      ripple: true,
      // PrimeNG's own strings — the calendar, the paginator's aria labels, the empty-list text —
      // otherwise surface in English inside an otherwise 繁體中文 UI.
      translation: {
        dayNames: ['星期日', '星期一', '星期二', '星期三', '星期四', '星期五', '星期六'],
        dayNamesShort: ['週日', '週一', '週二', '週三', '週四', '週五', '週六'],
        dayNamesMin: ['日', '一', '二', '三', '四', '五', '六'],
        monthNames: ['1月', '2月', '3月', '4月', '5月', '6月', '7月', '8月', '9月', '10月', '11月', '12月'],
        monthNamesShort: ['1月', '2月', '3月', '4月', '5月', '6月', '7月', '8月', '9月', '10月', '11月', '12月'],
        today: '今天',
        clear: '清除',
        weekHeader: '週',
        chooseDate: '選擇日期',
        chooseYear: '選擇年份',
        chooseMonth: '選擇月份',
        prevYear: '上一年',
        nextYear: '下一年',
        prevMonth: '上個月',
        nextMonth: '下個月',
        prevDecade: '上一個十年',
        nextDecade: '下一個十年',
        emptyMessage: '查無資料',
        emptyFilterMessage: '查無符合的選項',
        emptySearchMessage: '查無結果',
        accept: '是',
        reject: '否',
        aria: {
          close: '關閉',
          previous: '上一個',
          next: '下一個',
          navigation: '導覽',
          pageLabel: '第 {page} 頁',
          firstPageLabel: '第一頁',
          lastPageLabel: '最後一頁',
          nextPageLabel: '下一頁',
          prevPageLabel: '上一頁',
          rowsPerPageLabel: '每頁筆數',
          jumpToPageDropdownLabel: '跳至頁面',
          jumpToPageInputLabel: '跳至頁面',
          selectRow: '選取此列',
          unselectRow: '取消選取此列',
          selectAll: '全選',
          unselectAll: '取消全選',
          trueLabel: '是',
          falseLabel: '否',
          nullLabel: '未選擇',
          removeLabel: '移除',
          listLabel: '選項清單',
        },
      },
    }),
    MessageService,
    ConfirmationService,
  ],
};
