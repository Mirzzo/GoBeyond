import 'package:flutter/material.dart';

import '../../data/models/mentor_model.dart';
import '../../data/models/subscription_model.dart';
import '../../data/models/training_plan_model.dart';

class ProgramCategory {
  const ProgramCategory({
    required this.title,
    required this.subtitle,
    required this.icon,
    required this.accentColorValue,
  });

  final String title;
  final String subtitle;
  final IconData icon;
  final int accentColorValue;
}

class PlanDay {
  const PlanDay({
    required this.dayLabel,
    required this.title,
    required this.focus,
    required this.durationLabel,
    required this.summary,
    required this.mainBlocks,
    required this.coachNote,
    this.completed = false,
    this.recovery = false,
  });

  final String dayLabel;
  final String title;
  final String focus;
  final String durationLabel;
  final String summary;
  final List<String> mainBlocks;
  final String coachNote;
  final bool completed;
  final bool recovery;
}

class ActivityEntry {
  const ActivityEntry({
    required this.title,
    required this.subtitle,
    required this.whenLabel,
    required this.metric,
    this.positive = true,
  });

  final String title;
  final String subtitle;
  final String whenLabel;
  final String metric;
  final bool positive;
}

class ProgressMetric {
  const ProgressMetric({
    required this.label,
    required this.value,
    required this.trend,
    required this.accentColorValue,
  });

  final String label;
  final String value;
  final String trend;
  final int accentColorValue;
}

class MobileDemoData {
  static final mentors = <MentorModel>[
    MentorModel(
      id: 1,
      name: 'Lejla Kovac',
      category: 'Hybrid Strength',
      rating: 4.9,
      price: 49,
      headline: 'Structured coaching for people with messy schedules.',
      city: 'Sarajevo',
      about:
          'Lejla builds plans around work stress, limited recovery windows and sustainable strength progress.',
      specialties: const ['Strength base', 'Nutrition rhythm', 'Weekly check-ins'],
      nextStartLabel: 'Starts Monday',
      responseTimeLabel: '< 3h response',
      reviewQuote: 'The first coach who adjusted my plan when my week fell apart.',
      activeClients: 28,
      accentColorValue: 0xFFF2A541,
    ),
    MentorModel(
      id: 2,
      name: 'Amir Hadzic',
      category: 'Calisthenics',
      rating: 4.8,
      price: 39,
      headline: 'Bodyweight progress without random YouTube routines.',
      city: 'Mostar',
      about:
          'Amir focuses on skill progression, clean form and joint-friendly volume for consistent practice.',
      specialties: const ['Pull-up ladder', 'Core control', 'Mobility'],
      nextStartLabel: '2 spots left',
      responseTimeLabel: 'Same-day feedback',
      reviewQuote: 'I finally understood how to progress from beginner drills to real structure.',
      activeClients: 19,
      accentColorValue: 0xFF5DD6C0,
    ),
    MentorModel(
      id: 3,
      name: 'Mia Novak',
      category: 'Weightlifting',
      rating: 4.7,
      price: 59,
      headline: 'Technique-first lifting with calm, measurable progression.',
      city: 'Zagreb',
      about:
          'Mia works with intermediate lifters who want stronger technique, cleaner pulls and better volume tolerance.',
      specialties: const ['Snatch timing', 'Clean & jerk', 'Meet prep'],
      nextStartLabel: 'Assessment open',
      responseTimeLabel: 'Video review in 24h',
      reviewQuote: 'Her cues were precise and actually translated into the next session.',
      activeClients: 16,
      accentColorValue: 0xFF8FA8FF,
    ),
  ];

  static final currentSubscription = SubscriptionModel(
    id: 11,
    status: 'Active',
    mentorName: 'Lejla Kovac',
    planName: 'Hybrid Reset / 8 Weeks',
    paymentStatus: 'Paid until 19 Mar',
    renewalLabel: 'Renews 19 Mar',
    checkInDay: 'Wednesday check-in',
    progressLabel: 'Week 4 of 8',
  );

  static final currentPlan = TrainingPlanModel(
    id: 31,
    motivationalQuote: 'Consistency beats intensity when the calendar gets loud.',
    weekNumber: 4,
    focusTitle: 'Lower body power + recovery control',
    focusSummary:
        'This block tightens your squat pattern, keeps cardio short and protects recovery with capped volume.',
    completedSessions: 3,
    totalSessions: 5,
    nextSessionTitle: 'Tempo squat + posterior chain',
    nextSessionDuration: '58 min',
  );

  static const categories = <ProgramCategory>[
    ProgramCategory(
      title: 'Strength Plans',
      subtitle: 'Weekly structure, deloads and mentor feedback.',
      icon: Icons.fitness_center_rounded,
      accentColorValue: 0xFFF2A541,
    ),
    ProgramCategory(
      title: 'Habit Reset',
      subtitle: 'Nutrition, sleep rhythm and realistic adherence.',
      icon: Icons.track_changes_rounded,
      accentColorValue: 0xFF5DD6C0,
    ),
    ProgramCategory(
      title: 'Progress Reviews',
      subtitle: 'Monthly metrics, notes and plan changes.',
      icon: Icons.insights_rounded,
      accentColorValue: 0xFF8FA8FF,
    ),
  ];

  static const planDays = <PlanDay>[
    PlanDay(
      dayLabel: 'Mon',
      title: 'Squat Primer',
      focus: 'Strength',
      durationLabel: '58 min',
      summary: 'Build lower body tension, then finish with controlled carries.',
      mainBlocks: ['Tempo squat 5x4', 'RDL 4x8', 'Walking lunges 3x12', 'Farmer carry 4 rounds'],
      coachNote: 'Keep the last two squat reps technically identical to the first two.',
      completed: true,
    ),
    PlanDay(
      dayLabel: 'Tue',
      title: 'Zone 2 Reset',
      focus: 'Recovery',
      durationLabel: '32 min',
      summary: 'Short engine session to keep fatigue low and rhythm high.',
      mainBlocks: ['Bike 20 min easy', 'Hip mobility flow 10 min', 'Breathing reset 2 min'],
      coachNote: 'You should finish feeling better than when you started.',
      completed: true,
      recovery: true,
    ),
    PlanDay(
      dayLabel: 'Wed',
      title: 'Upper Push & Pull',
      focus: 'Hypertrophy',
      durationLabel: '51 min',
      summary: 'Volume-focused upper work paired with strict rest windows.',
      mainBlocks: ['Incline press 4x8', 'Chest-supported row 4x10', 'Lateral raise 3x15', 'Push-up finisher'],
      coachNote: 'Cap the session. More sets here will not improve the week.',
      completed: true,
    ),
    PlanDay(
      dayLabel: 'Thu',
      title: 'Check-in Walk',
      focus: 'Recovery',
      durationLabel: '25 min',
      summary: 'Movement break and weekly mentor check-in review.',
      mainBlocks: ['Walk 20 min', 'Read check-in notes', 'Sleep target review'],
      coachNote: 'Use today to protect Friday quality, not to chase extra work.',
      recovery: true,
    ),
    PlanDay(
      dayLabel: 'Fri',
      title: 'Power Circuit',
      focus: 'Conditioning',
      durationLabel: '46 min',
      summary: 'Short explosive intervals and loaded carries to finish the week.',
      mainBlocks: ['KB swing EMOM', 'Sled push rounds', 'Carry ladder', 'Cooldown stretch'],
      coachNote: 'Explosive does not mean frantic. Keep every rep sharp.',
    ),
  ];

  static const progressMetrics = <ProgressMetric>[
    ProgressMetric(
      label: 'Adherence',
      value: '86%',
      trend: '+8% vs last month',
      accentColorValue: 0xFFF2A541,
    ),
    ProgressMetric(
      label: 'Bodyweight',
      value: '73.4 kg',
      trend: '-0.6 kg in 30 days',
      accentColorValue: 0xFF5DD6C0,
    ),
    ProgressMetric(
      label: 'Sessions',
      value: '14',
      trend: '3 more than last month',
      accentColorValue: 0xFF8FA8FF,
    ),
  ];

  static const activityHistory = <ActivityEntry>[
    ActivityEntry(
      title: 'Completed power circuit',
      subtitle: 'Heart rate stayed inside target zone.',
      whenLabel: 'Today',
      metric: '46 min',
    ),
    ActivityEntry(
      title: 'Weekly check-in submitted',
      subtitle: 'Recovery score and bodyweight were updated.',
      whenLabel: 'Yesterday',
      metric: 'Coach reviewed',
    ),
    ActivityEntry(
      title: 'New personal best on tempo squat',
      subtitle: 'Form note: stronger brace on final set.',
      whenLabel: '2 days ago',
      metric: '+5 kg',
    ),
    ActivityEntry(
      title: 'Missed recovery walk',
      subtitle: 'Schedule conflict logged and plan auto-adjusted.',
      whenLabel: '5 days ago',
      metric: 'Replanned',
      positive: false,
    ),
  ];
}
