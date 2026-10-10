import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react-native";
import { beforeEach, describe, expect, it, jest } from "@jest/globals";
import { AppState } from "react-native";
import { router } from "expo-router";
import EventScreen from "../src/screens/EventScreen";
import DashboardScreen from "../src/screens/DashboardScreen";
import AvailabilityScreen from "../src/screens/AvailabilityScreen";
import { api } from "../src/lib/config";
import {
  ApiError,
  type Assignment,
  type DemoContext,
  type EventDetails,
} from "../src/lib/api";

jest.mock("../src/lib/config", () => ({
  api: {
    context: jest.fn(),
    dashboard: jest.fn(),
    event: jest.fn(),
    respond: jest.fn(),
    availability: jest.fn(),
    createAvailability: jest.fn(),
    updateAvailability: jest.fn(),
    deleteAvailability: jest.fn(),
  },
}));
const mock = jest.mocked(api);
const context: DemoContext = {
  organizationId: "cedar",
  organizationName: "Cedar Grove",
  displayName: "Alex Morgan",
  userId: "alex",
  timeZone: "America/Chicago",
  role: "Volunteer",
};
const pending: Assignment = {
  id: "assignment",
  eventId: "gathering",
  eventTitle: "Sunday gathering",
  eventStartsAt: "2099-04-18T14:00:00Z",
  eventEndsAt: "2099-04-18T16:00:00Z",
  teamName: "Welcome",
  roleName: "Door host",
  volunteerId: "alex",
  volunteerName: "Alex Morgan",
  status: "Pending",
  respondedAt: null,
  version: 3,
  isCurrentUser: true,
  conflicts: [
    {
      type: "Unavailable",
      message: "This assignment overlaps your unavailable time.",
      startsAt: "2099-04-18T15:00:00Z",
      endsAt: "2099-04-18T16:00:00Z",
      relatedEventId: null,
      relatedEventTitle: null,
      relatedAssignmentId: null,
      availabilityId: "away",
    },
  ],
};
let event: EventDetails;
beforeEach(() => {
  jest.resetAllMocks();
  jest
    .spyOn(AppState, "addEventListener")
    .mockReturnValue({ remove: jest.fn() });
  event = {
    id: "gathering",
    title: "Sunday gathering",
    description: "Make room around the table.",
    startsAt: pending.eventStartsAt,
    endsAt: pending.eventEndsAt,
    location: "Community room",
    assignments: [{ ...pending }],
  };
  mock.context.mockResolvedValue(context);
  mock.event.mockImplementation(async () => structuredClone(event));
  mock.availability.mockResolvedValue([]);
});

describe("native volunteer event workflow", () => {
  it("loads an event, shows team/status/server conflict, and preserves other volunteers' ownership", async () => {
    event.assignments.push({
      ...pending,
      id: "someone-else",
      roleName: "Coffee host",
      volunteerName: "Jamie Chen",
      isCurrentUser: false,
      conflicts: [],
    });
    await render(<EventScreen eventId="gathering" />);
    expect(await screen.findByText("Sunday gathering")).toBeOnTheScreen();
    expect(screen.getAllByText("Welcome")).toHaveLength(2);
    expect(screen.getByText("Alex Morgan · You")).toBeOnTheScreen();
    expect(screen.getByText("Schedule conflict")).toBeOnTheScreen();
    expect(
      screen.getByText("This assignment overlaps your unavailable time."),
    ).toBeOnTheScreen();
    expect(
      screen.getByRole("button", { name: "Accept Door host" }),
    ).toBeEnabled();
    expect(
      screen.queryByRole("button", { name: "Accept Coffee host" }),
    ).toBeNull();
    expect(mock.event).toHaveBeenCalledWith(
      "cedar",
      "gathering",
      expect.any(AbortSignal),
    );
  });

  it("saves Accept then Decline using the current version and reloads the saved reply", async () => {
    mock.respond.mockImplementation(
      async (_org, _id, decision, expectedVersion) => {
        const updated = {
          ...event.assignments[0],
          status: decision,
          version: expectedVersion + 1,
          conflicts: decision === "Declined" ? [] : pending.conflicts,
        };
        event.assignments = [updated];
        return updated;
      },
    );
    const view = await render(<EventScreen eventId="gathering" />);
    await fireEvent.press(
      await screen.findByRole("button", { name: "Accept Door host" }),
    );
    expect(mock.respond).toHaveBeenLastCalledWith(
      "cedar",
      "assignment",
      "Accepted",
      3,
    );
    expect(await screen.findByText("Accepted")).toBeOnTheScreen();
    expect(screen.getByText(/Your response is saved/)).toBeOnTheScreen();
    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Decline Door host" }),
      ).toBeEnabled(),
    );
    await fireEvent.press(
      screen.getByRole("button", { name: "Decline Door host" }),
    );
    expect(mock.respond).toHaveBeenLastCalledWith(
      "cedar",
      "assignment",
      "Declined",
      4,
    );
    expect(await screen.findByText("Declined")).toBeOnTheScreen();
    expect(screen.queryByText("Schedule conflict")).toBeNull();
    await view.unmount();
    await render(<EventScreen eventId="gathering" />);
    expect(await screen.findByText("Declined")).toBeOnTheScreen();
  });

  it("disables submission while saving and reports request errors without claiming success", async () => {
    let reject!: (error: Error) => void;
    mock.respond.mockImplementation(
      () =>
        new Promise((_resolve, rejectPromise) => {
          reject = rejectPromise;
        }),
    );
    await render(<EventScreen eventId="gathering" />);
    await fireEvent.press(
      await screen.findByRole("button", { name: "Accept Door host" }),
    );
    expect(screen.getByText("Saving response…")).toBeOnTheScreen();
    expect(
      screen.getByRole("button", { name: "Decline Door host" }),
    ).toBeDisabled();
    await act(async () => reject(new Error("Connection lost. Try again.")));
    expect(
      await screen.findByText("Connection lost. Try again."),
    ).toBeOnTheScreen();
    expect(screen.queryByText(/Your response is saved/)).toBeNull();
    expect(
      screen.getByRole("button", { name: "Accept Door host" }),
    ).toBeEnabled();
  });

  it("requires refreshing after a stale response and never automatically submits again", async () => {
    mock.respond.mockRejectedValue(
      new ApiError(409, "The schedule changed. Refresh first."),
    );
    await render(<EventScreen eventId="gathering" />);
    await fireEvent.press(
      await screen.findByRole("button", { name: "Accept Door host" }),
    );
    expect(
      screen.getByRole("button", { name: "Decline Door host" }),
    ).toBeDisabled();
    event.assignments = [{ ...pending, status: "Accepted", version: 7 }];
    await fireEvent.press(
      screen.getByRole("button", { name: "Refresh latest details" }),
    );
    expect(await screen.findByText("Accepted")).toBeOnTheScreen();
    expect(mock.respond).toHaveBeenCalledTimes(1);
  });

  it("shows initial loading, then an error with retry and a useful empty event", async () => {
    let reject!: (error: Error) => void;
    mock.event.mockImplementationOnce(
      () =>
        new Promise((_resolve, rejectPromise) => {
          reject = rejectPromise;
        }),
    );
    await render(<EventScreen eventId="gathering" />);
    expect(screen.getByText("Loading event…")).toBeOnTheScreen();
    await act(async () => reject(new Error("The API is unavailable.")));
    expect(
      await screen.findByText("The API is unavailable."),
    ).toBeOnTheScreen();
    event.assignments = [];
    await fireEvent.press(screen.getByRole("button", { name: "Try again" }));
    expect(
      await screen.findByText("No volunteers have been assigned yet."),
    ).toBeOnTheScreen();
  });

  it("clears the old event and reply controls when navigating to an unavailable event", async () => {
    const view = await render(<EventScreen eventId="gathering" />);
    await screen.findByRole("button", { name: "Accept Door host" });
    mock.event.mockRejectedValue(
      new Error("This event is no longer available."),
    );
    await view.rerender(<EventScreen eventId="another-event" />);
    expect(
      await screen.findByText("This event is no longer available."),
    ).toBeOnTheScreen();
    expect(screen.queryByText("Sunday gathering")).toBeNull();
    expect(
      screen.queryByRole("button", { name: "Accept Door host" }),
    ).toBeNull();
  });

  it("refreshes persisted status when returning from the background", async () => {
    const listener = jest.spyOn(AppState, "addEventListener");
    await render(<EventScreen eventId="gathering" />);
    await screen.findByText("Awaiting reply");
    event.assignments = [{ ...pending, status: "Accepted", version: 8 }];
    await act(async () => {
      listener.mock.calls.at(-1)![1]("active");
    });
    expect(await screen.findByText("Accepted")).toBeOnTheScreen();
    listener.mockRestore();
  });

  it("opens a native event route from the live dashboard", async () => {
    mock.dashboard.mockResolvedValue({
      organization: {
        id: "cedar",
        name: "Cedar Grove",
        timeZone: context.timeZone,
      },
      currentUser: { id: "alex", displayName: "Alex Morgan" },
      summary: {
        upcomingEvents: 1,
        pendingResponses: 1,
        acceptedAssignments: 0,
      },
      events: [event],
      assignments: [pending],
    });
    await render(<DashboardScreen />);
    await fireEvent.press(
      await screen.findByRole("button", { name: "Open Sunday gathering" }),
    );
    expect(router.push).toHaveBeenCalledWith({
      pathname: "/events/[id]",
      params: { id: "gathering" },
    });
    expect(screen.getByText("Schedule conflict")).toBeOnTheScreen();
  });
});

describe("native availability", () => {
  it("validates ranges, creates unavailable time, and shows save feedback", async () => {
    mock.createAvailability.mockResolvedValue({
      id: "away",
      startsAt: "2099-04-18T09:00:00Z",
      endsAt: "2099-04-18T10:00:00Z",
      note: "Family plans",
      version: 1,
    });
    await render(<AvailabilityScreen />);
    await fireEvent.press(
      await screen.findByRole("button", { name: "Add unavailable time" }),
    );
    await fireEvent.changeText(
      screen.getByLabelText("Unavailable start"),
      "2099-04-18 10:00",
    );
    await fireEvent.changeText(
      screen.getByLabelText("Unavailable end"),
      "2099-04-18 09:00",
    );
    await fireEvent.press(
      screen.getByRole("button", { name: "Save unavailable time" }),
    );
    expect(screen.getByText("End must be later than start.")).toBeOnTheScreen();
    expect(mock.createAvailability).not.toHaveBeenCalled();
    await fireEvent.changeText(
      screen.getByLabelText("Unavailable end"),
      "2099-04-18 11:00",
    );
    await fireEvent.changeText(
      screen.getByLabelText("Private note"),
      "  Family plans  ",
    );
    await fireEvent.press(
      screen.getByRole("button", { name: "Save unavailable time" }),
    );
    expect(mock.createAvailability).toHaveBeenCalledWith("cedar", {
      startsAt: new Date(2099, 3, 18, 10).toISOString(),
      endsAt: new Date(2099, 3, 18, 11).toISOString(),
      note: "Family plans",
    });
    expect(
      await screen.findByText(/Your unavailable time is saved/),
    ).toBeOnTheScreen();
  });

  it("edits using optimistic version and requires explicit confirmation before removal", async () => {
    const entry = {
      id: "away",
      startsAt: "2027-11-07T07:30:37Z",
      endsAt: "2027-11-07T08:30:42Z",
      note: "Old note",
      version: 4,
    };
    mock.availability.mockResolvedValue([entry]);
    mock.updateAvailability.mockResolvedValue({
      ...entry,
      note: "Updated note",
      version: 5,
    });
    mock.deleteAvailability.mockResolvedValue(undefined);
    await render(<AvailabilityScreen />);
    await fireEvent.press(
      await screen.findByRole("button", {
        name: /^Edit unavailable time starting/,
      }),
    );
    await fireEvent.changeText(
      screen.getByLabelText("Private note"),
      "Updated note",
    );
    await fireEvent.press(
      screen.getByRole("button", { name: "Save unavailable time" }),
    );
    expect(mock.updateAvailability).toHaveBeenCalledWith(
      "cedar",
      "away",
      { startsAt: entry.startsAt, endsAt: entry.endsAt, note: "Updated note" },
      4,
    );
    await fireEvent.press(
      await screen.findByRole("button", {
        name: /^Remove unavailable time starting/,
      }),
    );
    expect(mock.deleteAvailability).not.toHaveBeenCalled();
    await fireEvent.press(
      screen.getByRole("button", { name: "Confirm removal" }),
    );
    expect(mock.deleteAvailability).toHaveBeenCalledWith("cedar", "away", 4);
    expect(
      await screen.findByText(/Unavailable time removed/),
    ).toBeOnTheScreen();
  });
});
