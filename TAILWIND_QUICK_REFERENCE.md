# Bootstrap → Tailwind Quick Reference

## Buttons
| Bootstrap | Tailwind |
|-----------|----------|
| `btn btn-primary` | `btn-primary` |
| `btn btn-secondary` | `btn-secondary` |
| `btn btn-success` | `btn-success` |
| `btn btn-danger` | `btn-danger` |
| `btn btn-warning` | `btn-warning` |
| `btn btn-sm` | `btn-sm` |
| `btn btn-lg` | `btn-lg` |
| `btn-close` | `<Icon Name="x" Class="w-5 h-5" />` |

## Layout
| Bootstrap | Tailwind |
|-----------|----------|
| `container` | `container mx-auto px-4` |
| `row` | `flex flex-wrap -mx-2` or `grid grid-cols-12 gap-4` |
| `col-md-6` | `w-full md:w-1/2` or use grid |
| `d-flex` | `flex` |
| `d-none` | `hidden` |
| `d-block` | `block` |
| `flex-column` | `flex-col` |
| `flex-row` | `flex-row` |
| `justify-content-center` | `justify-center` |
| `justify-content-between` | `justify-between` |
| `align-items-center` | `items-center` |
| `align-items-start` | `items-start` |

## Spacing
| Bootstrap | Tailwind |
|-----------|----------|
| `mt-1` | `mt-1` |
| `mt-2` | `mt-2` |
| `mt-3` | `mt-3` |
| `mt-4` | `mt-4` |
| `mt-5` | `mt-5` |
| `mb-3` | `mb-3` |
| `ms-2` | `ms-2` |
| `me-2` | `me-2` |
| `p-3` | `p-3` |
| `px-4` | `px-4` |
| `py-2` | `py-2` |
| `m-auto` | `mx-auto` |
| `gap-2` | `gap-2` |

## Typography
| Bootstrap | Tailwind |
|-----------|----------|
| `h1` | `text-3xl font-bold` |
| `h2` | `text-2xl font-bold` |
| `h3` | `text-xl font-bold` |
| `h4` | `text-lg font-bold` |
| `h5` | `text-base font-bold` |
| `lead` | `text-lg font-light` |
| `text-muted` | `text-gray-600 dark:text-gray-400` |
| `text-primary` | `text-primary-600 dark:text-primary-400` |
| `text-danger` | `text-danger-600 dark:text-danger-400` |
| `text-center` | `text-center` |
| `text-end` | `text-right` |
| `fw-bold` | `font-bold` |
| `fw-normal` | `font-normal` |
| `fst-italic` | `italic` |
| `text-decoration-none` | `no-underline` |

## Forms
| Bootstrap | Tailwind |
|-----------|----------|
| `form-label` | `form-label` |
| `form-control` | `form-input` |
| `form-select` | `form-select` |
| `form-check-input` (checkbox) | `form-checkbox` |
| `form-check-input` (radio) | `form-radio` |
| `form-check-label` | `ml-2 text-sm` |
| `input-group` | `flex` |
| `invalid-feedback` | `form-error` |
| `valid-feedback` | `text-success-600 text-sm mt-1` |
| `form-text` | `form-hint` |

## Cards
| Bootstrap | Tailwind |
|-----------|----------|
| `card` | `card` |
| `card-header` | `card-header` |
| `card-body` | `card-body` |
| `card-footer` | `card-footer` |
| `card-title` | `text-lg font-semibold` |
| `card-text` | `text-gray-700 dark:text-gray-300` |

## Alerts
| Bootstrap | Tailwind |
|-----------|----------|
| `alert alert-primary` | `alert-info` |
| `alert alert-success` | `alert-success` |
| `alert alert-warning` | `alert-warning` |
| `alert alert-danger` | `alert-danger` |
| `alert-dismissible` | Add close button manually |

## Badges
| Bootstrap | Tailwind |
|-----------|----------|
| `badge bg-primary` | `badge-primary` |
| `badge bg-secondary` | `badge-secondary` |
| `badge bg-success` | `badge-success` |
| `badge bg-danger` | `badge-danger` |
| `badge bg-warning` | `badge-warning` |

## Colors
| Bootstrap | Tailwind |
|-----------|----------|
| `bg-primary` | `bg-primary-600 dark:bg-primary-500` |
| `bg-secondary` | `bg-gray-200 dark:bg-gray-700` |
| `bg-success` | `bg-success-600` |
| `bg-danger` | `bg-danger-600` |
| `bg-warning` | `bg-warning-600` |
| `bg-light` | `bg-gray-100 dark:bg-gray-800` |
| `bg-dark` | `bg-gray-800 dark:bg-gray-200` |
| `bg-white` | `bg-white dark:bg-gray-900` |

## Borders
| Bootstrap | Tailwind |
|-----------|----------|
| `border` | `border border-gray-300 dark:border-gray-700` |
| `border-top` | `border-t border-gray-300 dark:border-gray-700` |
| `border-0` | `border-0` |
| `rounded` | `rounded-lg` |
| `rounded-pill` | `rounded-full` |
| `rounded-circle` | `rounded-full` |

## Shadows & Effects
| Bootstrap | Tailwind |
|-----------|----------|
| `shadow-sm` | `shadow-sm` |
| `shadow` | `shadow-md` |
| `shadow-lg` | `shadow-lg` |
| `opacity-50` | `opacity-50` |

## Display & Visibility
| Bootstrap | Tailwind |
|-----------|----------|
| `d-none d-md-block` | `hidden md:block` |
| `d-block d-md-none` | `block md:hidden` |
| `invisible` | `invisible` |
| `visible` | `visible` |
| `overflow-auto` | `overflow-auto` |
| `overflow-hidden` | `overflow-hidden` |

## Position
| Bootstrap | Tailwind |
|-----------|----------|
| `position-relative` | `relative` |
| `position-absolute` | `absolute` |
| `position-fixed` | `fixed` |
| `position-sticky` | `sticky` |
| `top-0` | `top-0` |
| `bottom-0` | `bottom-0` |
| `start-0` | `left-0` |
| `end-0` | `right-0` |

## Width & Height
| Bootstrap | Tailwind |
|-----------|----------|
| `w-25` | `w-1/4` |
| `w-50` | `w-1/2` |
| `w-75` | `w-3/4` |
| `w-100` | `w-full` |
| `w-auto` | `w-auto` |
| `h-100` | `h-full` |
| `vh-100` | `h-screen` |
| `vw-100` | `w-screen` |
| `mw-100` | `max-w-full` |
| `mh-100` | `max-h-full` |

## Tables
| Bootstrap | Tailwind |
|-----------|----------|
| `table` | `table` |
| `table-striped` | Add `even:bg-gray-50 dark:even:bg-gray-800` to tbody tr |
| `table-hover` | Add `hover:bg-gray-50 dark:hover:bg-gray-800` to tbody tr |
| `table-bordered` | `border-collapse border` |

## Navigation
| Bootstrap | Tailwind |
|-----------|----------|
| `navbar` | Custom nav structure |
| `navbar-brand` | `text-xl font-bold` |
| `nav-link` | `px-3 py-2 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-800` |
| `nav-link active` | `bg-primary-100 dark:bg-primary-900 text-primary-600 dark:text-primary-400` |
| `navbar-toggler` | `lg:hidden` (mobile menu button) |

## Icons (Bootstrap Icons → Heroicons)
| Bootstrap Icon | Heroicon Name |
|----------------|---------------|
| `bi-house` | `home` |
| `bi-person` | `user` |
| `bi-gear` | `cog` or `settings` |
| `bi-calendar` | `calendar` |
| `bi-clock` | `clock` |
| `bi-trash` | `trash` |
| `bi-pencil` | `pencil` or `edit` |
| `bi-plus` | `plus` |
| `bi-x` | `x` or `close` |
| `bi-check` | `check` |
| `bi-search` | `magnifying-glass` |
| `bi-list` | `bars-3` |
| `bi-tags` | `tag` |
| `bi-bell` | `bell` |
| `bi-download` | `arrow-download` |
| `bi-upload` | `arrow-upload` |
| `bi-sun` | `sun` |
| `bi-moon` | `moon` |
| `bi-filter` | `filter` |
| `bi-info-circle` | `information-circle` |
| `bi-exclamation-triangle` | `exclamation-triangle` |

## Modal Usage

### Bootstrap (Old)
```razor
<!-- Trigger -->
<button class="btn btn-primary" data-bs-toggle="modal" data-bs-target="#myModal">
    Open Modal
</button>

<!-- Modal -->
<div class="modal fade" id="myModal" tabindex="-1">
    <div class="modal-dialog">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title">Title</h5>
                <button class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body">Content</div>
        </div>
    </div>
</div>
```

### Tailwind (New)
```razor
<!-- Trigger -->
<button class="btn-primary" @onclick="() => showModal = true">
    Open Modal
</button>

<!-- Modal -->
@if (showModal)
{
    <div class="modal-overlay" @onclick="() => showModal = false">
        <div class="modal-content max-w-lg" @onclick:stopPropagation="true">
            <div class="modal-header">
                <h5 class="text-lg font-semibold">Title</h5>
                <button @onclick="() => showModal = false">
                    <Icon Name="x" Class="w-5 h-5" />
                </button>
            </div>
            <div class="modal-body">Content</div>
        </div>
    </div>
}

@code {
    private bool showModal = false;
}
```

## Responsive Breakpoints

| Bootstrap | Tailwind | Viewport |
|-----------|----------|----------|
| No prefix | No prefix | < 640px (mobile) |
| `sm-` | `sm:` | ≥ 640px |
| `md-` | `md:` | ≥ 768px |
| `lg-` | `lg:` | ≥ 1024px |
| `xl-` | `xl:` | ≥ 1280px |
| `xxl-` | `2xl:` | ≥ 1536px |

Example:
```html
<!-- Bootstrap -->
<div class="col-12 col-md-6 col-lg-4">

<!-- Tailwind -->
<div class="w-full md:w-1/2 lg:w-1/3">
```
